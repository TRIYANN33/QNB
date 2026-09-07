namespace QNB;

internal sealed class OperationSourcesForm : Form
{
    private readonly DataGridView _grid;
    private readonly Label _summary;
    private List<BankAccountProfile> _accounts = new();

    public OperationSourcesForm()
    {
        Text = "QNB - Sources des opérations";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1050, 650);
        MinimumSize = new Size(850, 520);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.FromArgb(4, 36, 73), Padding = new Padding(20, 12, 20, 8) };
        header.Controls.Add(new Label { Text = "Sources des opérations", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold), Location = new Point(20, 12) });
        header.Controls.Add(new Label { Text = "Gérez les banques et comptes utilisés lors des importations.", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Location = new Point(22, 49) });
        _summary = new Label { AutoSize = true, ForeColor = Color.FromArgb(58, 196, 187), Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(820, 31) };
        header.Controls.Add(_summary);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.FromArgb(7, 42, 78),
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
        };
        AddColumn("Banque", 180);
        AddColumn("Nom du compte", 190);
        AddColumn("Référence / IBAN", 220);
        AddColumn("Titulaire", 190);
        AddColumn("Type", 130);
        _grid.CellDoubleClick += (_, _) => EditSelected();

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = Color.FromArgb(4, 36, 73) };
        var close = MakeButton("Fermer", Color.FromArgb(51, 73, 99), 110);
        close.DialogResult = DialogResult.Cancel;
        var delete = MakeButton("Supprimer", Color.FromArgb(166, 61, 76), 120);
        var edit = MakeButton("Modifier", Color.FromArgb(8, 73, 137), 120);
        var add = MakeButton("Nouvelle source", Color.FromArgb(34, 149, 255), 145);
        delete.Click += (_, _) => DeleteSelected();
        edit.Click += (_, _) => EditSelected();
        add.Click += (_, _) => EditAccount(null);
        footer.Controls.Add(close); footer.Controls.Add(delete); footer.Controls.Add(edit); footer.Controls.Add(add);

        Controls.Add(_grid);
        Controls.Add(footer);
        Controls.Add(header);
        Shown += (_, _) => Reload();
    }

    private void AddColumn(string title, int width) => _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, Width = width });

    private void Reload()
    {
        _accounts = BankingRepository.LoadConfiguration().Accounts;
        _grid.Rows.Clear();
        foreach (var account in _accounts)
            _grid.Rows.Add(account.BankName, account.AccountName, account.AccountReference, account.Holder, TypeText(account.Type));
        _summary.Text = $"{_accounts.Count} source(s)";
    }

    private BankAccountProfile? SelectedAccount()
    {
        if (_grid.CurrentRow is null) return null;
        var index = _grid.CurrentRow.Index;
        return index >= 0 && index < _accounts.Count ? _accounts[index] : null;
    }

    private void EditSelected()
    {
        var account = SelectedAccount();
        if (account is not null) EditAccount(account);
    }

    private void EditAccount(BankAccountProfile? existing)
    {
        using var form = new SourceEditorForm(existing);
        if (form.ShowDialog(this) != DialogResult.OK || form.Account is null) return;
        BankingRepository.SaveAccount(form.Account);
        Reload();
    }

    private void DeleteSelected()
    {
        var account = SelectedAccount();
        if (account is null) return;
        var imports = BankingRepository.LoadImports().Count(x => x.AccountId == account.Id);
        if (imports > 0)
        {
            MessageBox.Show($"Cette source est liée à {imports} import(s). Elle ne peut pas être supprimée afin de préserver l'historique des opérations.", "QNB - Source utilisée", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show($"Supprimer la source « {account.DisplayName} » ?", "QNB - Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var configuration = BankingRepository.LoadConfiguration();
        configuration.Accounts.RemoveAll(x => x.Id == account.Id);
        BankingRepository.SaveConfiguration(configuration);
        Reload();
    }

    private static string TypeText(BankAccountType type) => type switch
    {
        BankAccountType.Courant => "Compte courant",
        BankAccountType.Epargne => "Épargne",
        BankAccountType.CarteDifferee => "Carte différée",
        _ => "Autre"
    };

    private static Button MakeButton(string text, Color backColor, int width) => new()
    {
        Text = text, Width = width, Height = 36, BackColor = backColor, ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false
    };
}

internal sealed class SourceEditorForm : Form
{
    private readonly TextBox _bank = new();
    private readonly TextBox _name = new();
    private readonly TextBox _reference = new();
    private readonly TextBox _holder = new();
    private readonly ComboBox _type = new();
    private readonly Guid _id;

    public BankAccountProfile? Account { get; private set; }

    public SourceEditorForm(BankAccountProfile? existing)
    {
        _id = existing?.Id ?? Guid.NewGuid();
        Text = existing is null ? "QNB - Nouvelle source" : "QNB - Modifier la source";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(600, 430);
        MinimumSize = new Size(600, 430);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 6, BackColor = BackColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _bank.Text = existing?.BankName ?? string.Empty;
        _name.Text = existing?.AccountName ?? string.Empty;
        _reference.Text = existing?.AccountReference ?? string.Empty;
        _holder.Text = existing?.Holder ?? string.Empty;
        _type.DropDownStyle = ComboBoxStyle.DropDownList;
        _type.Items.AddRange(new object[] { "Compte courant", "Épargne", "Carte différée", "Autre" });
        _type.SelectedIndex = existing?.Type switch { BankAccountType.Epargne => 1, BankAccountType.CarteDifferee => 2, BankAccountType.Autre => 3, _ => 0 };

        AddRow(layout, 0, "Banque", _bank);
        AddRow(layout, 1, "Nom du compte", _name);
        AddRow(layout, 2, "Référence / IBAN", _reference);
        AddRow(layout, 3, "Titulaire", _holder);
        AddRow(layout, 4, "Type de compte", _type);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0) };
        var save = new Button { Text = "Enregistrer", Width = 125, Height = 36, BackColor = Color.FromArgb(34, 149, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Annuler", Width = 110, Height = 36, BackColor = Color.FromArgb(51, 73, 99), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 5); layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save; CancelButton = cancel;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_bank.Text) || string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show("Renseignez au minimum la banque et le nom du compte.", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Account = new BankAccountProfile
        {
            Id = _id, BankName = _bank.Text.Trim(), AccountName = _name.Text.Trim(),
            AccountReference = _reference.Text.Trim(), Holder = _holder.Text.Trim(),
            Type = _type.SelectedIndex switch { 1 => BankAccountType.Epargne, 2 => BankAccountType.CarteDifferee, 3 => BankAccountType.Autre, _ => BankAccountType.Courant }
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, Control control)
    {
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(183, 207, 229), TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
