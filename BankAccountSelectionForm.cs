namespace QNB;

internal sealed class BankAccountSelectionForm : Form
{
    private readonly ComboBox _existingAccounts;
    private readonly TextBox _bankName;
    private readonly TextBox _accountName;
    private readonly TextBox _accountReference;
    private readonly TextBox _holder;
    private readonly ComboBox _accountType;
    private readonly DateTimePicker _sourceDate;

    public BankAccountProfile? SelectedAccount { get; private set; }

    public BankAccountSelectionForm(BankImportResult detected)
    {
        Text = "QNB - Banque et compte";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 550);
        MinimumSize = new Size(620, 550);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var configuration = BankingRepository.LoadConfiguration();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 9, BackColor = BackColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _existingAccounts = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _existingAccounts.Items.Add("Créer une nouvelle source");
        foreach (var account in configuration.Accounts) _existingAccounts.Items.Add(account);
        _existingAccounts.DisplayMember = nameof(BankAccountProfile.DisplayName);
        _existingAccounts.SelectedIndex = 0;
        _existingAccounts.SelectedIndexChanged += (_, _) => LoadSelectedExisting();

        _bankName = CreateTextBox(detected.BankName);
        _accountName = CreateTextBox(string.IsNullOrWhiteSpace(detected.AccountReference) ? "Compte principal" : detected.AccountReference);
        _accountReference = CreateTextBox(detected.AccountReference);
        _holder = CreateTextBox(detected.AccountHolder);
        _accountType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _accountType.Items.AddRange(new object[] { "Compte courant", "Épargne", "Carte différée", "Autre" });
        _accountType.SelectedIndex = 0;

        _sourceDate = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true };
        if (detected.BalanceDate.HasValue)
        {
            _sourceDate.Value = detected.BalanceDate.Value.Date;
            _sourceDate.Checked = true;
        }
        else
        {
            _sourceDate.Value = DateTime.Today;
            _sourceDate.Checked = false;
        }

        AddRow(layout, 0, "Date du solde / source", _sourceDate);
        AddRow(layout, 1, "Banque", _bankName);
        AddRow(layout, 2, "Nom du compte", _accountName);
        AddRow(layout, 3, "Référence / IBAN", _accountReference);
        AddRow(layout, 4, "Titulaire", _holder);
        AddRow(layout, 5, "Type de compte", _accountType);
        AddRow(layout, 6, "Source existante", _existingAccounts);

        var note = new Label
        {
            Text = detected.BalanceDate.HasValue
                ? "Date source détectée automatiquement à partir de la date du solde du relevé. Vous pouvez la corriger si nécessaire."
                : "Date du solde non détectée. Cochez la date et renseignez-la manuellement avant de continuer.",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(183, 207, 229), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(note, 0, 7); layout.SetColumnSpan(note, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
        var ok = CreateButton("Continuer", Color.FromArgb(34, 149, 255));
        var cancel = CreateButton("Annuler", Color.FromArgb(51, 73, 99));
        ok.Click += (_, _) => SaveAndClose(); cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 8); layout.SetColumnSpan(buttons, 2);
        AcceptButton = ok; CancelButton = cancel; Controls.Add(layout);
    }

    private void LoadSelectedExisting()
    {
        if (_existingAccounts.SelectedItem is not BankAccountProfile account) return;
        if (account.SourceDate.HasValue) { _sourceDate.Value = account.SourceDate.Value.Date; _sourceDate.Checked = true; }
        else _sourceDate.Checked = false;
        _bankName.Text = account.BankName; _accountName.Text = account.AccountName; _accountReference.Text = account.AccountReference; _holder.Text = account.Holder;
        _accountType.SelectedIndex = account.Type switch { BankAccountType.Courant => 0, BankAccountType.Epargne => 1, BankAccountType.CarteDifferee => 2, _ => 3 };
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_bankName.Text) || string.IsNullOrWhiteSpace(_accountName.Text))
        { MessageBox.Show("Renseignez au minimum la banque et le nom du compte.", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!_sourceDate.Checked)
        { MessageBox.Show("La date du solde n'a pas été détectée. Renseignez la date de la source manuellement.", "QNB - Date source", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var existing = _existingAccounts.SelectedItem as BankAccountProfile;
        if (existing is null)
        {
            var duplicate = SourceDataService.FindDuplicateSource(_bankName.Text, _accountName.Text, _accountReference.Text, _sourceDate.Value.Date);
            if (duplicate is not null)
            {
                MessageBox.Show($"La source « {duplicate.DisplayName} » existe déjà pour le {_sourceDate.Value:dd/MM/yyyy}.\n\nL'importation est annulée. Sélectionnez la source existante ou vérifiez la date du solde.", "QNB - Source déjà existante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SelectedAccount = null; DialogResult = DialogResult.Cancel; Close(); return;
            }
        }

        var account = existing ?? new BankAccountProfile();
        account.SourceDate = _sourceDate.Value.Date; account.BankName = _bankName.Text.Trim(); account.AccountName = _accountName.Text.Trim(); account.AccountReference = _accountReference.Text.Trim(); account.Holder = _holder.Text.Trim();
        account.Type = _accountType.SelectedIndex switch { 0 => BankAccountType.Courant, 1 => BankAccountType.Epargne, 2 => BankAccountType.CarteDifferee, _ => BankAccountType.Autre };
        SelectedAccount = BankingRepository.SaveAccount(account); DialogResult = DialogResult.OK; Close();
    }

    private static TextBox CreateTextBox(string text) => new() { Text = text, Dock = DockStyle.Fill };
    private static void AddRow(TableLayoutPanel layout, int row, string caption, Control control) { layout.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(183, 207, 229) }, 0, row); layout.Controls.Add(control, 1, row); }
    private static Button CreateButton(string text, Color color) => new() { Text = text, Width = 125, Height = 36, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
}
