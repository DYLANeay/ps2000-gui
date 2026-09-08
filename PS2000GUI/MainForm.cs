namespace PS2000GUI;

public class MainForm : Form
{
    readonly PS2000 _dev = new PS2000();

    // System.Windows.Forms.Timer et non System.Timers.Timer : celui-ci se déclenche sur le
    // thread de l'interface, on peut donc toucher aux contrôles sans Invoke
    readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();

    readonly TableLayoutPanel _grid;
    readonly Label _lblType, _lblSerial, _lblArticle, _lblMaxV, _lblActualV, _lblActualI, _lblStatus;
    readonly Label _lblRemote, _lblOutput;
    readonly Button _btnRemote, _btnOutput, _btnSet, _btnGet, _btnReconnect;
    readonly NumericUpDown _numSetpoint;

    string _connText = "";        // texte de connexion, réaffiché à chaque tic
    bool _remoteOn, _outputOn;    // dernier état lu, sert à savoir dans quel sens basculer

    public MainForm()
    {
        Text = "PS 2000 B Control";
        FormBorderStyle = FormBorderStyle.FixedSingle;  // pas de logique de redimensionnement à écrire
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // la fenêtre se dimensionne sur son contenu : pas de taille en dur à réajuster
        // à chaque ligne ajoutée, et rien n'est coupé quel que soit le zoom Windows
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // 2 colonnes : libellé à gauche, valeur à droite
        // pas de Dock ici : avec Dock.Fill le panneau prend la taille de la fenêtre et
        // l'AutoSize de la fenêtre n'a plus rien à mesurer, donc le contenu se retrouve coupé
        _grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(_grid);

        // le panneau remplit de gauche à droite et passe à la ligne tous les 2 contrôles
        Label AddRow(string caption)
        {
            var value = new Label { AutoSize = true, Text = "—", Margin = new Padding(3, 6, 3, 3) };
            _grid.Controls.Add(new Label { AutoSize = true, Text = caption, Margin = new Padding(3, 6, 3, 3) });
            _grid.Controls.Add(value);
            return value;
        }

        // une cellule ne contient qu'un contrôle : on en empile plusieurs dans un FlowLayoutPanel
        void AddRowOf(string caption, params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            foreach (var c in controls) flow.Controls.Add(c);
            _grid.Controls.Add(new Label { AutoSize = true, Text = caption, Margin = new Padding(3, 9, 3, 3) });
            _grid.Controls.Add(flow);
        }

        void AddHeader(string text)
        {
            var h = new Label { AutoSize = true, Text = text, Margin = new Padding(3, 12, 3, 3),
                                Font = new Font(Font, FontStyle.Bold) };
            _grid.Controls.Add(h);
            _grid.SetColumnSpan(h, 2);
        }

        _lblType    = AddRow("Device type");
        _lblSerial  = AddRow("Serial number");
        _lblArticle = AddRow("Article number");
        _lblMaxV    = AddRow("Max voltage");
        _lblActualV = AddRow("Actual voltage");
        _lblActualI = AddRow("Actual current");

        // la tension mesurée est la valeur que l'on regarde : on l'agrandit
        _lblActualV.Font = new Font("Segoe UI", 16, FontStyle.Bold);

        AddHeader("Control");

        _lblRemote = new Label { AutoSize = true, Text = "—", Width = 40, Margin = new Padding(3, 7, 14, 3) };
        _btnRemote = new Button { Text = "Turn remote ON", AutoSize = true };
        _btnRemote.Click += OnRemoteClick;
        AddRowOf("Remote", _lblRemote, _btnRemote);

        _lblOutput = new Label { AutoSize = true, Text = "—", Width = 40, Margin = new Padding(3, 7, 14, 3) };
        _btnOutput = new Button { Text = "Turn output ON", AutoSize = true };
        _btnOutput.Click += OnOutputClick;
        AddRowOf("Output", _lblOutput, _btnOutput);

        // NumericUpDown refuse tout seul les valeurs hors bornes : aucune validation à écrire
        _numSetpoint = new NumericUpDown { DecimalPlaces = 2, Increment = 0.5m, Width = 80,
                                           Minimum = 0, Maximum = 84 };
        _btnSet = new Button { Text = "Set", AutoSize = true };
        _btnGet = new Button { Text = "Get", AutoSize = true };
        _btnSet.Click += OnSetClick;
        _btnGet.Click += OnGetClick;
        AddRowOf("Setpoint", _numSetpoint,
                 new Label { AutoSize = true, Text = "V", Margin = new Padding(3, 7, 10, 3) },
                 _btnSet, _btnGet);

        AddHeader("");

        _lblStatus = new Label { AutoSize = true, Text = "…" };
        _grid.Controls.Add(_lblStatus);
        _grid.SetColumnSpan(_lblStatus, 2);   // s'ajoute d'abord, puis on l'étale sur 2 colonnes

        _btnReconnect = new Button { Text = "Reconnect", AutoSize = true };
        _btnReconnect.Click += (s, e) => ConnectAndFill();
        _grid.Controls.Add(_btnReconnect);
        _grid.SetColumnSpan(_btnReconnect, 2);

        _timer.Interval = 500;
        _timer.Tick += OnTick;
    }

    // OnLoad et non le constructeur : le balayage des ports peut durer une seconde
    // ou deux, et la fenêtre doit déjà être affichée pendant ce temps
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ConnectAndFill();
    }

    // appelée au démarrage et par le bouton Reconnect
    void ConnectAndFill()
    {
        _timer.Stop();
        try
        {
            _dev.Connect();

            // lus une seule fois : ces valeurs ne changent pas tant que l'appareil est le même
            _lblType.Text    = _dev.ReadString(0);
            _lblSerial.Text  = _dev.ReadString(1);
            _lblArticle.Text = _dev.ReadString(6);

            // déjà obtenue par Connect(), qui s'en sert comme sonde
            _lblMaxV.Text = $"{_dev.NominalVoltage:0.00} V";
            _numSetpoint.Maximum = (decimal)_dev.NominalVoltage;

            _connText = _dev.IsOffline
                ? "OFFLINE — simulated values"
                : $"Connected on {_dev.PortName}";
            _lblStatus.Text = _connText;

            _timer.Start();
        }
        catch (Exception ex)
        {
            // un appareil débranché ne doit jamais faire planter la démonstration
            _lblStatus.Text = ex.Message;
        }
    }

    // une seule interrogation de l'objet 71 par tic : état + tension + courant
    void OnTick(object? sender, EventArgs e)
    {
        try
        {
            byte[] d = _dev.Query(71, 6);

            _remoteOn = (d[0] & 0x01) != 0;
            _outputOn = (d[1] & 0x01) != 0;

            // valeurs transmises en pourcentage du nominal, poids fort en premier
            int uRaw = (d[2] << 8) | d[3];
            int iRaw = (d[4] << 8) | d[5];

            _lblActualV.Text = $"{uRaw / 25600.0 * _dev.NominalVoltage:0.00} V";
            _lblActualI.Text = $"{iRaw / 25600.0 * _dev.NominalCurrent:0.000} A";

            // le bouton porte l'action à faire, le libellé porte l'état courant
            _lblRemote.Text = _remoteOn ? "ON" : "OFF";
            _lblOutput.Text = _outputOn ? "ON" : "OFF";
            _btnRemote.Text = _remoteOn ? "Turn remote OFF" : "Turn remote ON";
            _btnOutput.Text = _outputOn ? "Turn output OFF" : "Turn output ON";

            // état brut affiché pour vérifier les bits au labo depuis la face avant
            _lblStatus.Text = $"{_connText} — status 0x{d[0]:X2}{d[1]:X2}";
        }
        catch (Exception ex)
        {
            // on arrête le minuteur, sinon l'erreur se répète toutes les 500 ms
            _timer.Stop();
            _lblStatus.Text = ex.Message;
        }
    }

    // évite de répéter le même try/catch dans les quatre gestionnaires
    void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) { _lblStatus.Text = ex.Message; }
    }

    // objet 54 : masque puis valeur. Un seul bit à la fois (§3.1.5)
    void OnRemoteClick(object? sender, EventArgs e) =>
        Try(() => _dev.Write(54, [0x10, _remoteOn ? (byte)0x00 : (byte)0x10]));

    void OnOutputClick(object? sender, EventArgs e) =>
        Try(() => _dev.Write(54, [0x01, _outputOn ? (byte)0x00 : (byte)0x01]));

    void OnSetClick(object? sender, EventArgs e) => Try(() =>
    {
        ushort raw = (ushort)Math.Round((double)_numSetpoint.Value / _dev.NominalVoltage * 25600.0);
        _dev.Write(50, [(byte)(raw >> 8), (byte)raw]);
    });

    void OnGetClick(object? sender, EventArgs e) => Try(() =>
    {
        byte[] d = _dev.Query(50, 2);
        double volts = ((d[0] << 8) | d[1]) / 25600.0 * _dev.NominalVoltage;
        _numSetpoint.Value = Math.Clamp((decimal)volts, _numSetpoint.Minimum, _numSetpoint.Maximum);
    });
}
