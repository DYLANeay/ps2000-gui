using PS2000Lib;

using System.Globalization;
using System.Text;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace PS2000GUI;

public class MainForm : Form
{
    readonly IPowerSupply _powerSupply = PowerSupplyFactory.ConnectOrDummy();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    readonly FlowLayoutPanel _panel = new()
    {
        FlowDirection = FlowDirection.TopDown,
        AutoSize = true,
        Padding = new Padding(10),
    };

    readonly TextBox _txtBroker = new() { Text = "127.0.0.1", Width = 100 };
    readonly TextBox _txtPsuId = new() { Text = "DUMMY001", Width = 100 };
    readonly Button _btnConnect = new() { AutoSize = true, Text = "Connect" };
    MqttClient? _client;
    string _prefix = "";
    string _lastSetpoint = "0";

    readonly Label _lblType = new() { AutoSize = true };
    readonly Label _lblSerial = new() { AutoSize = true };
    readonly Label _lblArticle = new() { AutoSize = true };
    readonly Label _lblMaxV = new() { AutoSize = true };
    readonly Label _lblActualV = new() { AutoSize = true };
    readonly Label _lblStatus = new() { AutoSize = true };
    readonly Button _btnRemote = new() { AutoSize = true, Text = "Remote: ?" };
    readonly Button _btnOutput = new() { AutoSize = true, Text = "Output: ?" };
    readonly NumericUpDown _numSetpoint = new()
    {
        DecimalPlaces = 2,
        Increment = 0.5m,
        Width = 80,
    };
    readonly Button _btnSet = new() { AutoSize = true, Text = "Set" };
    readonly Button _btnGet = new() { AutoSize = true, Text = "Get" };

    bool _remoteOn,
        _outputOn;

    public MainForm()
    {
        Text = "PS 200 Control";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var connectRow = new FlowLayoutPanel { AutoSize = true };
        connectRow.Controls.AddRange([
            new Label { Text = "Broker:", AutoSize = true },
            _txtBroker,
            new Label { Text = "PSU ID:", AutoSize = true },
            _txtPsuId,
            _btnConnect,
        ]);

        var setpointRow = new FlowLayoutPanel { AutoSize = true };
        setpointRow.Controls.AddRange([
            new Label { Text = "Setpoint (V):", AutoSize = true },
            _numSetpoint,
            _btnSet,
            _btnGet,
        ]);

        _panel.Controls.AddRange([
            connectRow,
            _lblType,
            _lblSerial,
            _lblArticle,
            _lblMaxV,
            _lblActualV,
            _btnRemote,
            _btnOutput,
            setpointRow,
            _lblStatus,
        ]);
        Controls.Add(_panel);

        // les boutons basculent l'état actuel ; l'affichage change quand le PSU republie son status
        _btnRemote.Click += (s, e) => Try(() => SendCommand("cmd/remote", _remoteOn ? "OFF" : "ON"));
        _btnOutput.Click += (s, e) => Try(() => SendCommand("cmd/output", _outputOn ? "OFF" : "ON"));

        _btnSet.Click += (s, e) =>
            Try(() =>
            {
                SendCommand("cmd/voltage", _numSetpoint.Value.ToString(CultureInfo.InvariantCulture));
            });

        // plus d'alim à interroger : on reprend la dernière consigne reçue sur status/setpoint
        _btnGet.Click += (s, e) =>
            Try(() =>
            {
                _numSetpoint.Value = decimal.Parse(_lastSetpoint, CultureInfo.InvariantCulture);
            });

        _btnConnect.Click += (s, e) => Try(ConnectBroker);

        _timer.Tick += (s, e) => Try(Poll);
        UpdateEnabled();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Try(() =>
        {
            _lblType.Text = "Device type: " + _powerSupply.DeviceType;
            _lblSerial.Text = "Serial number: " + _powerSupply.SerialNumber;
            _lblArticle.Text = "Article number: " + _powerSupply.ArticleNumber;
            _lblMaxV.Text = $"Max voltage: {_powerSupply.NominalVoltage:0.00} V";
            _numSetpoint.Maximum = (decimal)_powerSupply.NominalVoltage;
            _lblStatus.Text = "Connected on " + _powerSupply.PortName;
            _timer.Start();
        });
    }

    // même enchaînement que btnConnect_Click + btnSubscribe_Click dans l'exemple du prof
    void ConnectBroker()
    {
        _prefix = $"/PSU/PSU2000/{_txtPsuId.Text}/";
        _client = new MqttClient(_txtBroker.Text);
        // handler branché avant Subscribe : les messages retained arrivent dès l'abonnement
        _client.MqttMsgPublishReceived += OnMessage;
        _client.Connect(Guid.NewGuid().ToString());
        _client.Subscribe(new string[] { _prefix + "#" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        _timer.Stop(); // l'état arrive maintenant par MQTT, plus besoin de poller l'alim
        _lblStatus.Text = "Connected to broker " + _txtBroker.Text;
    }

    // une commande n'est jamais retained, sinon le broker la rejouerait au redémarrage du PSU
    void SendCommand(string command, string payload)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to the broker");
        _client.Publish(_prefix + command, Encoding.UTF8.GetBytes(payload), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        _lblStatus.Text = ""; // une nouvelle commande part : on efface l'ancienne erreur
    }

    // appelé par un thread de M2Mqtt : on repasse sur le thread UI avant de toucher aux contrôles
    void OnMessage(object sender, MqttMsgPublishEventArgs e)
    {
        string topic = e.Topic.Substring(_prefix.Length); // ex. "status/voltage"
        string payload = Encoding.UTF8.GetString(e.Message); // ex. "12.50"
        Invoke((MethodInvoker)(() => ShowMessage(topic, payload)));
    }

    // les cmd/* qu'on reçoit aussi (abonné à #) ne tombent dans aucun case
    void ShowMessage(string topic, string payload)
    {
        switch (topic)
        {
            case "info/type":
                _lblType.Text = "Device type: " + payload;
                break;
            case "info/serial":
                _lblSerial.Text = "Serial number: " + payload;
                break;
            case "info/article":
                _lblArticle.Text = "Article number: " + payload;
                break;
            case "info/nominal":
                _lblMaxV.Text = $"Max voltage: {payload} V";
                _numSetpoint.Maximum = decimal.Parse(payload, CultureInfo.InvariantCulture);
                break;
            case "status/voltage":
                _lblActualV.Text = $"Actual voltage: {payload} V";
                break;
            case "status/remote":
                _remoteOn = payload == "ON";
                _btnRemote.Text = "Remote: " + payload;
                break;
            case "status/output":
                _outputOn = payload == "ON";
                _btnOutput.Text = "Output: " + payload;
                break;
            case "status/setpoint":
                _lastSetpoint = payload;
                break;
            case "error":
                _lblStatus.Text = payload;
                break;
        }
        UpdateEnabled();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // sans Disconnect les threads de M2Mqtt gardent le processus en vie
        _client?.Disconnect();
        base.OnFormClosing(e);
    }

    void Poll()
    {
        PsuStatus status = _powerSupply.ReadStatus();
        _remoteOn = status.RemoteOn;
        _outputOn = status.OutputOn;
        _lblActualV.Text = $"Actual voltage: {status.ActualVoltage:0.00} V";
        _btnRemote.Text = "Remote: " + (_remoteOn ? "ON" : "OFF");
        _btnOutput.Text = "Output: " + (_outputOn ? "ON" : "OFF");
        UpdateEnabled();
    }

    // sans remote l'appareil refuse les écritures, donc on grise
    void UpdateEnabled() => _btnOutput.Enabled = _btnSet.Enabled = _numSetpoint.Enabled = _remoteOn;

    // une erreur série s'affiche en bas au lieu de faire planter l'app
    void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = ex.Message;
        }
    }
}
