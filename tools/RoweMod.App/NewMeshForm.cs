namespace RoweMod.App;

sealed class NewMeshForm : Form
{
    readonly TextBox _name = new();
    readonly ComboBox _slot = new();

    public string GarmentName => _name.Text.Trim();
    public string Slot => _slot.SelectedItem as string ?? "Tops";

    public NewMeshForm()
    {
        Text = "Create a garment";
        Width = 540;
        Height = 400;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10f);

        var hint = new Label
        {
            Left = 20,
            Top = 16,
            Width = 480,
            Height = 128,
            Text =
                "Blender will open with gray bones and no clothes. That is the real Rollout skeleton." +
                Environment.NewLine + Environment.NewLine +
                "Model the garment around those bones. Weight-paint the body bones (spine, arms, legs), not the ik helpers. Twist bones on the arms are real. Use them for sleeves." +
                Environment.NewLine + Environment.NewLine +
                "Tops and bottoms share this rig. Hats and hair do not.",
        };

        var nameLab = new Label { Left = 20, Top = 156, Width = 140, Text = "Name in the menu" };
        _name.Left = 160;
        _name.Top = 152;
        _name.Width = 340;
        _name.Text = "wide hoodie";

        var slotLab = new Label { Left = 20, Top = 196, Width = 140, Text = "Closet slot" };
        _slot.Left = 160;
        _slot.Top = 192;
        _slot.Width = 340;
        _slot.DropDownStyle = ComboBoxStyle.DropDownList;
        _slot.Items.AddRange(new object[] { "Tops", "Bottoms" });
        _slot.SelectedIndex = 0;

        var ok = new Button
        {
            Text = "Create and open Blender",
            Left = 160,
            Top = 260,
            Width = 210,
            Height = 36,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 110, 80),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
        };
        ok.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 110);
        var cancel = new Button
        {
            Text = "Cancel",
            Left = 380,
            Top = 260,
            Width = 120,
            Height = 36,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 70, 90),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
        };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { hint, nameLab, _name, slotLab, _slot, ok, cancel });
    }
}
