namespace RoweMod.App;

sealed class NewMeshForm : Form
{
    readonly TextBox _name = new();
    readonly ComboBox _slot = new();
    readonly WorkshopDomain _domain;

    public string GarmentName => _name.Text.Trim();
    public string Slot => _slot.SelectedItem as string ?? (_domain == WorkshopDomain.Skates ? "Frames" : "Tops");

    public NewMeshForm(WorkshopDomain domain = WorkshopDomain.Clothing)
    {
        _domain = domain;
        var skate = domain == WorkshopDomain.Skates;
        Text = skate ? "New skate" : "New garment";
        Width = 500;
        Height = 330;
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
            Width = 440,
            Height = 80,
            Text = skate
                ? "Creates a saved Blender project from a frame or boot.\nEdit in Blender → Ctrl+S → Export + Play.\nWheels use Paint."
                : "Creates a saved Blender project with the game rig.\nModel and weight your garment → Ctrl+S → Export + Play.",
        };

        var nameLab = new Label { Left = 20, Top = 110, Width = 140, Text = "Mod name" };
        _name.Left = 160;
        _name.Top = 106;
        _name.Width = 300;
        _name.PlaceholderText = skate ? "e.g. My frame" : "e.g. My hoodie";

        var slotLab = new Label { Left = 20, Top = 154, Width = 140, Text = "Slot" };
        _slot.Left = 160;
        _slot.Top = 150;
        _slot.Width = 300;
        _slot.DropDownStyle = ComboBoxStyle.DropDownList;
        if (skate)
            _slot.Items.AddRange(new object[] { "Frames", "Boots" });
        else
            _slot.Items.AddRange(new object[] { "Tops", "Bottoms" });
        _slot.SelectedIndex = 0;

        var ok = new Button
        {
            Text = "Create + open Blender",
            Left = 20,
            Top = 226,
            Width = 260,
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
            Left = 320,
            Top = 226,
            Width = 120,
            Height = 36,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 70, 90),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
        };

        AcceptButton = ok;
        ok.Enabled = false;
        _name.TextChanged += (_, _) => ok.Enabled = !string.IsNullOrWhiteSpace(GarmentName);
        CancelButton = cancel;
        Controls.AddRange(new Control[] { hint, nameLab, _name, slotLab, _slot, ok, cancel });
    }
}
