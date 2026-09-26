using System.IO.Ports;
using System.Text;

namespace PS2000Lib;

public class PS2000
{
    SerialPort? _port;

    public float NominalVoltage { get; private set; }
    public string DeviceType { get; private set; } = "";
    public string SerialNumber { get; private set; } = "";
    public string ArticleNumber { get; private set; } = "";

    public void Connect()
    {
        _port = new SerialPort("COM3", 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 500 };
        _port.Open();
        NominalVoltage = ReadFloat(2); // à lire en premier, toutes les valeurs en % en dépendent

        // objets 0, 1, 6 : chaînes ASCII de 16 octets, fixes pour l'appareil, lues une seule fois
        DeviceType = ReadString(0);
        SerialNumber = ReadString(1);
        ArticleNumber = ReadString(6);
    }

    private byte[] Send(byte sd, byte obj, byte[] data)
    {
        // trame : SD, DN (toujours 0), OBJ, DATA, puis la somme des octets sur 2 octets (poids fort d'abord)
        byte[] telegram = [sd, 0, obj, .. data, 0, 0];
        int checksum = telegram.Sum(b => (int)b);
        int lastIndex = telegram.Length - 1;
        telegram[lastIndex - 1] = (byte)(checksum >> 8); // poids fort
        telegram[lastIndex] = (byte)checksum; // poids faible

        _port!.DiscardInBuffer();
        _port.Write(telegram, 0, telegram.Length);

        // les 4 bits bas du SD de la réponse donnent la longueur des données moins 1
        int respSd = _port.ReadByte();
        int dataLen = (respSd & 0x0F) + 1;
        int frameLen = 3 + dataLen + 2; // SD + DN + OBJ + DATA + checksum (2 octets)

        byte[] frame = new byte[frameLen];
        frame[0] = (byte)respSd;
        _port.BaseStream.ReadExactly(frame, 1, frameLen - 1);

        Thread.Sleep(50); // l'appareil n'aime pas les requêtes trop rapprochées
        return frame;
    }

    private byte[] Query(byte obj, int expectLen)
    {
        // 0x70 = requête vers l'appareil, 0xF0 plus bas = écriture
        byte[] frame = Send((byte)(0x70 | (expectLen - 1)), obj, []);

        // si l'objet de la réponse vaut 0xFF, c'est une trame d'erreur
        if (frame[2] == 0xFF)
            throw new IOException($"Error 0x{frame[3]:X2} reading object {obj}");
        return frame[3..^2];
    }

    private void Write(byte obj, byte[] data)
    {
        byte[] frame = Send((byte)(0xF0 | (data.Length - 1)), obj, data);

        // 0x00 = ok, 0x09 = remote pas activé
        if (frame[3] != 0x00)
            throw new IOException($"Error 0x{frame[3]:X2} writing object {obj}");
    }

    public void SetRemote(bool on)
    {
        // objet 54 : un octet masque (quel bit on change) puis un octet valeur
        // 0x10 = remote, 0x01 = output
        Write(54, [0x10, (byte)(on ? 0x10 : 0)]);
    }

    public void SetOutput(bool on)
    {
        Write(54, [0x01, (byte)(on ? 0x01 : 0)]);
    }

    public void SetVoltage(double voltage)
    {
        ushort raw = ToRaw(voltage);
        Write(50, [(byte)(raw >> 8), (byte)raw]);
    }

    public double GetVoltageSetpoint()
    {
        byte[] d = Query(50, 2);
        return ToVolts(d[0], d[1]);
    }

    // objet 71 : octet 0 bit 0 = remote, octet 1 bit 0 = output, octets 2-3 = tension en %
    // un seul télégramme pour les trois valeurs, d'où le record plutôt que trois méthodes
    public PsuStatus ReadStatus()
    {
        byte[] d = Query(71, 6);
        bool remoteOn = (d[0] & 0x01) != 0;
        bool outputOn = (d[1] & 0x01) != 0;
        return new PsuStatus(remoteOn, outputOn, ToVolts(d[2], d[3]));
    }

    // les consignes sont en pourcentage du nominal : 25600 = 100 %
    private ushort ToRaw(double voltage) => (ushort)(voltage / NominalVoltage * 25600);

    private double ToVolts(byte high, byte low) => ((high << 8) | low) / 25600.0 * NominalVoltage;

    private string ReadString(byte obj) =>
        Encoding.ASCII.GetString(Query(obj, 16)).TrimEnd('\0', ' ');

    // l'appareil envoie le float en big-endian, on retourne les octets pour le PC
    private float ReadFloat(byte obj) => BitConverter.ToSingle(Query(obj, 4).Reverse().ToArray());
}
