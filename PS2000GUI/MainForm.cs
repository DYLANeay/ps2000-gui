using PS2000Lib;

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

        _btnRemote.Click += (s, e) => Try(() => _dev.SetRemote(!_remoteOn));
        _btnOutput.Click += (s, e) => Try(() => _dev.SetOutput(!_outputOn));

        _btnSet.Click += (s, e) => Try(() =>
        {
            _dev.SetVoltage((double)_numSetpoint.Value);
        });

        _btnGet.Click += (s, e) => Try(() =>
        {
            _numSetpoint.Value = (decimal)_dev.GetVoltageSetpoint();
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
            _lblType.Text = "Device type: " + _dev.DeviceType;
            _lblSerial.Text = "Serial number: " + _dev.SerialNumber;
            _lblArticle.Text = "Article number: " + _dev.ArticleNumber;
            _lblMaxV.Text = $"Max voltage: {_dev.NominalVoltage:0.00} V";
            _numSetpoint.Maximum = (decimal)_dev.NominalVoltage;
            _lblStatus.Text = "Connected on COM3";
            _timer.Start();
        });
    }

    void Poll()
    {
        PsuStatus status = _dev.ReadStatus();
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
        try { action(); }
        catch (Exception ex) { _lblStatus.Text = ex.Message; }
    }
}
