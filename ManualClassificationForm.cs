namespace QNB;

internal sealed class ManualClassificationForm : Form
{
    private readonly TextBox _typeBox;
    private readonly TextBox _subTypeBox;
    public string OperationType => _typeBox.Text.Trim();
    public string OperationSubType => _subTypeBox.Text.Trim();

    public ManualClassificationForm(string type, string subType)
    {
        Text = "QNB - Typage manuel";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(3, 23, 49); ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        Controls.Add(new Label { Text = "Type", AutoSize = true, Location = new Point(25, 28), ForeColor = Color.FromArgb(183,207,229) });
        _typeBox = new TextBox { Location = new Point(145, 24), Width = 280, Text = type };
        Controls.Add(_typeBox);
        Controls.Add(new Label { Text = "S_Type", AutoSize = true, Location = new Point(25, 78), ForeColor = Color.FromArgb(183,207,229) });
        _subTypeBox = new TextBox { Location = new Point(145, 74), Width = 280, Text = subType };
        Controls.Add(_subTypeBox);

        var save = new Button { Text = "Enregistrer", Location = new Point(285, 145), Width = 140, Height = 36, BackColor = Color.FromArgb(25,130,105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", Location = new Point(155, 145), Width = 115, Height = 36, DialogResult = DialogResult.Cancel };
        Controls.Add(save); Controls.Add(cancel);
        AcceptButton = save; CancelButton = cancel;
    }
}
