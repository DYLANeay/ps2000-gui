using System.IO.Ports;
using System.Text;

namespace PS2000GUI;

public class PS2000
{
    SerialPort? _port;

    public float NominalVoltage { get; private set; }
    public bool IsOffline { get; private set; }

    public void Connect()
    {
        try
        {
            _port = new SerialPort("COM3", 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 500 };
            _port.Open();
            NominalVoltage = ReadFloat(2);   // sert aussi de test de connexion
        }
        catch
        {
            _port?.Dispose();
            IsOffline = true;
            NominalVoltage = 84f;
        }
    }

    byte[] Send(byte sd, byte obj, byte[] data)
    {
        // trame : SD, DN (toujours 0), OBJ, DATA, puis la somme des octets sur 2 octets (poids fort d'abord)
        byte[] t = [sd, 0, obj, .. data, 0, 0];
        int sum = t.Sum(b => (int)b);
        t[^2] = (byte)(sum >> 8);
        t[^1] = (byte)sum;

        _port!.DiscardInBuffer();
        _port.Write(t, 0, t.Length);

        // les 4 bits bas du SD de la réponse donnent la longueur des données moins 1
        int respSd = _port.ReadByte();
        byte[] frame = new byte[3 + (respSd & 0x0F) + 1 + 2];
        frame[0] = (byte)respSd;
        _port.BaseStream.ReadExactly(frame, 1, frame.Length - 1);

        Thread.Sleep(50);   // l'appareil n'aime pas les requêtes trop rapprochées
        return frame;
    }

    public byte[] Query(byte obj, int expectLen)
    {
        if (IsOffline) return SimQuery(obj);

        // 0x70 = requête vers l'appareil, 0xF0 plus bas = écriture
        byte[] frame = Send((byte)(0x70 | (expectLen - 1)), obj, []);

        // si l'objet de la réponse vaut 0xFF, c'est une trame d'erreur
        if (frame[2] == 0xFF)
            throw new IOException($"Error 0x{frame[3]:X2} reading object {obj}");
        return frame[3..^2];
    }

    public void Write(byte obj, byte[] data)
    {
        if (IsOffline) { SimWrite(obj, data); return; }

        byte[] frame = Send((byte)(0xF0 | (data.Length - 1)), obj, data);

        // 0x00 = ok, 0x09 = remote pas activé
        if (frame[3] != 0x00)
            throw new IOException($"Error 0x{frame[3]:X2} writing object {obj}");
    }

    public string ReadString(byte obj) => Encoding.ASCII.GetString(Query(obj, 16)).TrimEnd('\0', ' ');

    // l'appareil envoie le float en big-endian, on retourne les octets pour le PC
    public float ReadFloat(byte obj) => BitConverter.ToSingle(Query(obj, 4).Reverse().ToArray());

    // ----- appareil simulé pour tester sans l'alim, à supprimer plus tard -----

    bool _simRemote, _simOutput;
    ushort _simSetpoint;

    byte[] SimQuery(byte obj) => obj switch
    {
        0 => Encoding.ASCII.GetBytes("PS 2084-03 B (sim)"),
        1 => Encoding.ASCII.GetBytes("SIMULATED"),
        6 => Encoding.ASCII.GetBytes("06230210"),
        50 => [(byte)(_simSetpoint >> 8), (byte)_simSetpoint],
        71 => [(byte)(_simRemote ? 1 : 0), (byte)(_simOutput ? 1 : 0),
               (byte)(_simOutput ? _simSetpoint >> 8 : 0), (byte)(_simOutput ? _simSetpoint : 0)],
        _ => throw new IOException($"No object {obj} in simulation")
    };

    void SimWrite(byte obj, byte[] data)
    {
        if (!_simRemote && !(obj == 54 && (data[0] & 0x10) != 0))
            throw new IOException($"Error 0x09 writing object {obj}");

        if (obj == 54)
        {
            if ((data[0] & 0x10) != 0) _simRemote = (data[1] & 0x10) != 0;
            if ((data[0] & 0x01) != 0) _simOutput = (data[1] & 0x01) != 0;
        }
        else if (obj == 50)
            _simSetpoint = (ushort)((data[0] << 8) | data[1]);
    }
}
