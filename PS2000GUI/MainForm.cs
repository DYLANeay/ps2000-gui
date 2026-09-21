namespace PS2000GUI;

public class MainForm : Form
{
    readonly PS2000 _dev = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    readonly FlowLayoutPanel _panel = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10) };

    readonly Label _lblType = new() { AutoSize = true };
    readonly Label _lblSerial = new() { AutoSize = true };
    readonly Label _lblArticle = new() { AutoSize = true };
    readonly Label _lblMaxV = new() { AutoSize = true };
    readonly Label _lblActualV = new() { AutoSize = true };
    readonly Label _lblStatus = new() { AutoSize = true };
    readonly Button _btnRemote = new() { AutoSize = true, Text = "Remote: ?" };
    readonly Button _btnOutput = new() { AutoSize = true, Text = "Output: ?" };
    readonly NumericUpDown _numSetpoint = new() { DecimalPlaces = 2, Increment = 0.5m, Width = 80 };
    readonly Button _btnSet = new() { AutoSize = true, Text = "Set" };
    readonly Button _btnGet = new() { AutoSize = true, Text = "Get" };

    bool _remoteOn, _outputOn;

    public MainForm()
    {
        Text = "PS 200 Control";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var setpointRow = new FlowLayoutPanel { AutoSize = true };
        setpointRow.Controls.AddRange([new Label { Text = "Setpoint (V):", AutoSize = true }, _numSetpoint, _btnSet, _btnGet]);

        _panel.Controls.AddRange([_lblType, _lblSerial, _lblArticle, _lblMaxV, _lblActualV,
                                  _btnRemote, _btnOutput, setpointRow, _lblStatus]);
        Controls.Add(_panel);

        // objet 54 : un octet masque (quel bit on change) puis un octet valeur
        // 0x10 = remote, 0x01 = output
        _btnRemote.Click += (s, e) => Try(() => _dev.Write(54, [0x10, (byte)(_remoteOn ? 0 : 0x10)]));
        _btnOutput.Click += (s, e) => Try(() => _dev.Write(54, [0x01, (byte)(_outputOn ? 0 : 0x01)]));

        // les consignes sont en pourcentage du nominal : 25600 = 100 %
        _btnSet.Click += (s, e) => Try(() =>
        {
            ushort raw = (ushort)((double)_numSetpoint.Value / _dev.NominalVoltage * 25600);
            _dev.Write(50, [(byte)(raw >> 8), (byte)raw]);
        });
        _btnGet.Click += (s, e) => Try(() =>
        {
            byte[] d = _dev.Query(50, 2);
            _numSetpoint.Value = (decimal)(((d[0] << 8) | d[1]) / 25600.0 * _dev.NominalVoltage);
        });

        _timer.Tick += (s, e) => Try(Poll);
        UpdateEnabled();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Try(() =>
        {
            _dev.Connect();
            _lblType.Text = "Device type: " + _dev.ReadString(0);
            _lblSerial.Text = "Serial number: " + _dev.ReadString(1);
            _lblArticle.Text = "Article number: " + _dev.ReadString(6);
            _lblMaxV.Text = $"Max voltage: {_dev.NominalVoltage:0.00} V";
            _numSetpoint.Maximum = (decimal)_dev.NominalVoltage;
            _lblStatus.Text = "Connected on COM3";
            _timer.Start();
        });
    }

    void Poll()
    {
        // objet 71 : octet 0 bit 0 = remote, octet 1 bit 0 = output, octets 2-3 = tension en %
        byte[] d = _dev.Query(71, 6);
        _remoteOn = (d[0] & 0x01) != 0;
        _outputOn = (d[1] & 0x01) != 0;
        _lblActualV.Text = $"Actual voltage: {((d[2] << 8) | d[3]) / 25600.0 * _dev.NominalVoltage:0.00} V";
        _btnRemote.Text = "Remote: " + (_remoteOn ? "ON" : "OFF");
        _btnOutput.Text = "Output: " + (_outputOn ? "ON" : "OFF");
        UpdateEnabled();
    }

    // sans remote l'appareil refuse les écritures (erreur 0x09), donc on grise
    void UpdateEnabled() => _btnOutput.Enabled = _btnSet.Enabled = _numSetpoint.Enabled = _remoteOn;

    // une erreur série s'affiche en bas au lieu de faire planter l'app
    void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) { _lblStatus.Text = ex.Message; }
    }
}
