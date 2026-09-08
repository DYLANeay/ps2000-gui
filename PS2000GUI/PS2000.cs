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

        public string PortName { get; private set; } = "";

        public float NominalVoltage { get; private set; }
        public bool IsOffline { get; private set; }

        // état de l'appareil simulé, utilisé seulement hors ligne
        bool _simRemote;
        bool _simOutput;
        ushort _simSetpointRaw;

        public void Connect()
        {
            _port?.Dispose();          // pour que le bouton Reconnect fonctionne
            _port = null;
            IsOffline = false;

            foreach (string name in SerialPort.GetPortNames().OrderBy(n => n == "COM3" ? 0 : 1))
            {
                try
                {
                    var port = new SerialPort(name, 115200, Parity.None, 8, StopBits.One);
                    port.ReadTimeout = 500;
                    port.Open();
                    _port = port;

                    NominalVoltage = ReadFloat(2);   // sonde la liaison ET donne la valeur nominale
                    PortName = name;
                    return;
                }
                catch (Exception)
                {
                    _port?.Dispose();
                    _port = null;
                }
            }

            IsOffline = true;
            NominalVoltage = 84f;
            PortName = "OFFLINE";
        }



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
            if (IsOffline) return SimulatedQuery(obj);

            byte sd = (byte)(0x70 | (expectLen - 1));
            byte[] frame = Transact(BuildTelegram(sd, obj, []));

            // objet 0xFF = trame d'erreur, pas des données
            if (frame[2] == 0xFF)
                throw new IOException($"Device returned error 0x{frame[3]:X2} on query of object {obj}");

            return frame[3..^2];
        }

        public void Write(byte obj, byte[] data) {
            if (IsOffline) { SimulatedWrite(obj, data); return; }

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

        // --- appareil simulé (mode hors ligne) ------------------------------------
        // completely IA generated part for testing at home purposes
        // renvoie les octets tels que l'appareil les enverrait : le décodage normal
        // s'applique par-dessus, donc rien de spécial dans ReadString ni ReadFloat

        byte[] SimulatedQuery(byte obj) => obj switch
        {
            0 => Encoding.ASCII.GetBytes("PS 2084-03 B (sim)"),
            1 => Encoding.ASCII.GetBytes("SIMULATED"),
            6 => Encoding.ASCII.GetBytes("06230210"),
            2 => [0x42, 0xA8, 0x00, 0x00],                              // 84.0f en big-endian
            50 => [(byte)(_simSetpointRaw >> 8), (byte)_simSetpointRaw],
            71 => SimulatedActualValues(),
            _ => throw new IOException($"Simulated device has no object {obj}")
        };

        byte[] SimulatedActualValues()
        {
            // la tension mesurée suit la consigne quand la sortie est active
            ushort u = _simOutput ? _simSetpointRaw : (ushort)0;
            return
            [
                (byte)(_simRemote ? 0x01 : 0x00),   // état 1 : contrôle à distance
                (byte)(_simOutput ? 0x01 : 0x00),   // état 2 : sortie DC
                (byte)(u >> 8), (byte)u,            // tension réelle
                0x00, 0x00                          // courant réel, toujours 0 en simulation
            ];
        }

        void SimulatedWrite(byte obj, byte[] data)
        {
            bool enablingRemote = obj == 54 && (data[0] & 0x10) != 0;

            // même règle que l'appareil : rien n'est accepté hors contrôle à distance
            if (!_simRemote && !enablingRemote)
                throw new IOException($"Device returned error 0x09 on write to object {obj}");

            switch (obj)
            {
                case 54:
                    byte mask = data[0], value = data[1];
                    if ((mask & 0x10) != 0) _simRemote = (value & 0x10) != 0;
                    if ((mask & 0x01) != 0) _simOutput = (value & 0x01) != 0;
                    break;

                case 50:
                    _simSetpointRaw = (ushort)((data[0] << 8) | data[1]);
                    break;

                default:
                    throw new IOException($"Simulated device has no object {obj}");
            }
        }


    }
}
