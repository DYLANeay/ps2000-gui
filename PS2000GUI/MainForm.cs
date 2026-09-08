namespace PS2000GUI;

public class MainForm : Form
{
    readonly PS2000 _dev = new PS2000();

    readonly TableLayoutPanel _grid;
    readonly Label _lblType, _lblSerial, _lblArticle, _lblMaxV, _lblActualV, _lblActualI, _lblStatus;

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
        _lblActualV = AddRow("Actual voltage");   // remplis par le minuteur à l'étape 5
        _lblActualI = AddRow("Actual current");

        _lblStatus = new Label { AutoSize = true, Text = "…" };
        _grid.Controls.Add(_lblStatus);
        _grid.SetColumnSpan(_lblStatus, 2);   // s'ajoute d'abord, puis on l'étale sur 2 colonnes
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

            _lblStatus.Text = _dev.IsOffline
                ? "OFFLINE — simulated values"
                : $"Connected on {_dev.PortName}";
        }
        catch (Exception ex)
        {
            // un appareil débranché ne doit jamais faire planter la démonstration
            _lblStatus.Text = ex.Message;
        }
    }
}
