using System.Globalization;
using System.Text;
using PS2000Lib;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// alim
IPowerSupply psu = PowerSupplyFactory.ConnectOrDummy();
Console.WriteLine($"Power supply: {psu.DeviceType} on {psu.PortName}");

// info du broker (mosquitto)
string broker = args.Length > 0 ? args[0] : "127.0.0.1"; 
string prefix = $"/PSU/PSU2000/{psu.SerialNumber}/";

//client MQTT
MqttClient client = new MqttClient(broker);
string clientID = Guid.NewGuid().ToString();
client.Connect(clientID);

//les infos fixes de l'appareil
Pub("info/type", psu.DeviceType, true);
Pub("info/serial", psu.SerialNumber, true);
Pub("info/article", psu.ArticleNumber, true);
Pub("info/nominal", psu.NominalVoltage.ToString("0.00", CultureInfo.InvariantCulture), true);

Console.WriteLine($"Publishing on {prefix} - press Enter to quit");
while (!Console.KeyAvailable) //jusqu'à ce que l'utilisateur appuie sur une touche
{
    // lecture de l'état de l'alim
    PsuStatus status = psu.ReadStatus();
    Pub("status/remote", status.RemoteOn ? "ON" : "OFF", true);
    Pub("status/output", status.OutputOn ? "ON" : "OFF", true);
    Pub("status/voltage", status.ActualVoltage.ToString("0.00", CultureInfo.InvariantCulture), true);
    Pub("status/setpoint", psu.GetVoltageSetpoint().ToString("0.00", CultureInfo.InvariantCulture), true);
    Thread.Sleep(1000);
}


client.Disconnect();

// helper pour ne pas répéter l'encodage et le QoS
void Pub(string topic, string payload, bool retain)
{
    //mqtt transports : UTF-8 encoding, Quality of Service level exactly once
    client.Publish(prefix + topic, Encoding.UTF8.GetBytes(payload), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, retain);
}