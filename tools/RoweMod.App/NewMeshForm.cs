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
        Height = 280;
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
            Height = 56,
            Text = skate
                ? "Opens a copy of a game frame or boot. Wheels are Paint."
                : "Opens the skeleton. Model clothes on it.",
        };

        var nameLab = new Label { Left = 20, Top = 84, Width = 140, Text = "Name" };
        _name.Left = 160;
        _name.Top = 80;
        _name.Width = 300;
        _name.Text = skate ? "wide frame" : "wide hoodie";

        var slotLab = new Label { Left = 20, Top = 124, Width = 140, Text = "Slot" };
        _slot.Left = 160;
        _slot.Top = 120;
        _slot.Width = 340;
        _slot.DropDownStyle = ComboBoxStyle.DropDownList;
        if (skate)
            _slot.Items.AddRange(new object[] { "Frames", "Boots" });
        else
            _slot.Items.AddRange(new object[] { "Tops", "Bottoms" });
        _slot.SelectedIndex = 0;

        var ok = new Button
        {
            Text = "Create",
            Left = 160,
            Top = 180,
            Width = 140,
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
            Top = 180,
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
