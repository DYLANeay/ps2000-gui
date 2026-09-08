using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PS2000GUI
{
    public class PS2000
    {
        SerialPort? _port;

        public float NominalVoltage { get; private set; }
        public bool IsOffline { get; private set; }

        static void ReadExactly(SerialPort port, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = port.Read(buffer, offset, count);
                if (read == 0)
                    throw new Exception("Unexpected end of stream");
                offset += read;
                count -= read;
            }
        }

        byte[] BuildTelegram(byte sd, byte obj, byte[] data) {
            List<byte> telegram = new List<byte>();
            telegram.Add(sd); // SD
            telegram.Add(0x00); // DN
            telegram.Add(obj); // OBJ
            telegram.AddRange(data); // DATA

            int checksum = telegram.Sum(b => (int)b);
            telegram.Add((byte)(checksum >> 8)); // poids fort
            telegram.Add((byte)checksum);        // poids faible

            return telegram.ToArray();
        }

        byte[] Transact(byte[] telegram)
        {
            if (_port == null || !_port.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            // vide les octets restés d'une transaction ratée
            _port.DiscardInBuffer();

            _port.Write(telegram, 0, telegram.Length);

            // lit un seul octet ; lève TimeoutException si pas d'appareil
            int sd = _port.ReadByte(); // Start Delimiter

            // les 4 bits bas du SD portent (longueur - 1)
            int datalen = (sd & 0x0F) + 1;

            // SD + DN + OBJ + DATA + 2 octets de somme de contrôle
            byte[] frame = new byte[3 + datalen + 2];

            // le SD est déjà lu : on le remet en 0 et on lit la suite à partir de 1
            frame[0] = (byte)sd;
            ReadExactly(_port, frame, 1, frame.Length - 1);

            Thread.Sleep(50);

            return frame;
        }

        public byte[] Query(byte obj, int expectLen) {
            byte sd = (byte)(0x70 | (expectLen - 1));
            byte[] frame = Transact(BuildTelegram(sd, obj, []));

            // objet 0xFF = trame d'erreur, pas des données
            if (frame[2] == 0xFF)
                throw new IOException($"Device returned error 0x{frame[3]:X2} on query of object {obj}");

            return frame[3..^2];
        }

        public void Write(byte obj, byte[] data) {
            byte sd = (byte)(0xF0 | (data.Length - 1));
            byte[] frame = Transact(BuildTelegram(sd, obj, data));

            // accusé de réception : 0x00 = succès, 0x09 = pas en contrôle à distance
            if (frame[3] != 0x00)
                throw new IOException($"Device returned error 0x{frame[3]:X2} on write to object {obj}");
        }

        public string ReadString(byte obj) {
            byte[] bytes = Query(obj, 16);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0', ' ');
        }

        public float ReadFloat(byte obj) {
            // l'appareil envoie en big-endian, le PC lit en little-endian
            byte[] bytes = Query(obj, 4).Reverse().ToArray();
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
