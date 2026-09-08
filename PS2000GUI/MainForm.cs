namespace PS2000GUI;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "PS 2000 B Control";
        ClientSize = new Size(420, 380);
        FormBorderStyle = FormBorderStyle.FixedSingle;  // pas de logique de redimensionnement à écrire
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // TODO step 4: TableLayoutPanel + AddRow helper + the control rows.
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Step 1 OK — build the layout here."
        });
    }
}
