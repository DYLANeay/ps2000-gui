using System.IO.Ports;
using System.Text;

namespace PS2000GUI;

public class PS2000
{
    SerialPort? _port;

    public float NominalVoltage { get; private set; }

    public void Connect()
    {
        _port = new SerialPort("COM3", 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 500 };
        _port.Open();
        NominalVoltage = ReadFloat(2);   // à lire en premier, toutes les valeurs en % en dépendent
    }

    byte[] Send(byte sd, byte obj, byte[] data)
    {
        // trame : SD, DN (toujours 0), OBJ, DATA, puis la somme des octets sur 2 octets (poids fort d'abord)
        byte[] telegram = [sd, 0, obj, .. data, 0, 0];
        int checksum = telegram.Sum(b => (int)b);
        int lastIndex = telegram.Length - 1;
        telegram[lastIndex - 1] = (byte)(checksum >> 8);   // poids fort
        telegram[lastIndex] = (byte)checksum;              // poids faible

        _port!.DiscardInBuffer();
        _port.Write(telegram, 0, telegram.Length);

        // les 4 bits bas du SD de la réponse donnent la longueur des données moins 1
        int respSd = _port.ReadByte();
        int dataLen = (respSd & 0x0F) + 1;
        int frameLen = 3 + dataLen + 2;   // SD + DN + OBJ + DATA + checksum (2 octets)

        byte[] frame = new byte[frameLen];
        frame[0] = (byte)respSd;
        _port.BaseStream.ReadExactly(frame, 1, frameLen - 1);

        Thread.Sleep(50);   // l'appareil n'aime pas les requêtes trop rapprochées
        return frame;
    }

    public byte[] Query(byte obj, int expectLen)
    {
        // 0x70 = requête vers l'appareil, 0xF0 plus bas = écriture
        byte[] frame = Send((byte)(0x70 | (expectLen - 1)), obj, []);

        // si l'objet de la réponse vaut 0xFF, c'est une trame d'erreur
        if (frame[2] == 0xFF)
            throw new IOException($"Error 0x{frame[3]:X2} reading object {obj}");
        return frame[3..^2];
    }

    public void Write(byte obj, byte[] data)
    {
        byte[] frame = Send((byte)(0xF0 | (data.Length - 1)), obj, data);

        // 0x00 = ok, 0x09 = remote pas activé
        if (frame[3] != 0x00)
            throw new IOException($"Error 0x{frame[3]:X2} writing object {obj}");
    }

    public string ReadString(byte obj) => Encoding.ASCII.GetString(Query(obj, 16)).TrimEnd('\0', ' ');

    // l'appareil envoie le float en big-endian, on retourne les octets pour le PC
    public float ReadFloat(byte obj) => BitConverter.ToSingle(Query(obj, 4).Reverse().ToArray());
}
