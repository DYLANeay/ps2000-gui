namespace PS2000GUI;

public class MainForm : Form
{
    readonly PS2000 _dev = new PS2000();

    // System.Windows.Forms.Timer et non System.Timers.Timer : celui-ci se déclenche sur le
    // thread de l'interface, on peut donc toucher aux contrôles sans Invoke
    readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();

    readonly TableLayoutPanel _grid;
    readonly Label _lblType, _lblSerial, _lblArticle, _lblMaxV, _lblActualV, _lblActualI, _lblStatus;

    string _connText = "";        // texte de connexion, réaffiché à chaque tic
    bool _remoteOn, _outputOn;    // dernier état lu, utilisé aux étapes 6 et 7

    public MainForm()
    {
        Text = "PS 2000 B Control";
        ClientSize = new Size(420, 380);
        FormBorderStyle = FormBorderStyle.FixedSingle;  // pas de logique de redimensionnement à écrire
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // 2 colonnes : libellé à gauche, valeur à droite
        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(_grid);

        // le panneau remplit de gauche à droite et passe à la ligne tous les 2 contrôles
        Label AddRow(string caption)
        {
            var value = new Label { AutoSize = true, Text = "—" };
            _grid.Controls.Add(new Label { AutoSize = true, Text = caption });
            _grid.Controls.Add(value);
            return value;
        }

        _lblType    = AddRow("Device type");
        _lblSerial  = AddRow("Serial number");
        _lblArticle = AddRow("Article number");
        _lblMaxV    = AddRow("Max voltage");
        _lblActualV = AddRow("Actual voltage");
        _lblActualI = AddRow("Actual current");

        // la tension mesurée est la valeur que l'on regarde : on l'agrandit
        _lblActualV.Font = new Font("Segoe UI", 16, FontStyle.Bold);

        _lblStatus = new Label { AutoSize = true, Text = "…" };
        _grid.Controls.Add(_lblStatus);
        _grid.SetColumnSpan(_lblStatus, 2);   // s'ajoute d'abord, puis on l'étale sur 2 colonnes

        _timer.Interval = 500;
        _timer.Tick += OnTick;
    }

    // OnLoad et non le constructeur : le balayage des ports peut durer une seconde
    // ou deux, et la fenêtre doit déjà être affichée pendant ce temps
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            _dev.Connect();

            // lus une seule fois : ces valeurs ne changent pas tant que l'appareil est le même
            _lblType.Text    = _dev.ReadString(0);
            _lblSerial.Text  = _dev.ReadString(1);
            _lblArticle.Text = _dev.ReadString(6);

            // déjà obtenue par Connect(), qui s'en sert comme sonde
            _lblMaxV.Text = $"{_dev.NominalVoltage:0.00} V";

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
}
