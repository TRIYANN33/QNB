namespace QNB;

internal sealed class OperationSourcesForm : Form
{
    private readonly DataGridView _grid;
    private readonly Label _summary;
    private List<BankAccountProfile> _accounts = new();
    private IReadOnlyList<MultiSortCriterion> _sortCriteria = Array.Empty<MultiSortCriterion>();

    public OperationSourcesForm()
    {
        Text = "QNB - Sources des opérations";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1150, 650);
        MinimumSize = new Size(900, 520);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.FromArgb(4, 36, 73), Padding = new Padding(20, 12, 20, 8) };
        header.Controls.Add(new Label { Text = "Sources des opérations", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold), Location = new Point(20, 12) });
        header.Controls.Add(new Label { Text = "Gérez les sources bancaires et leur date de relevé.", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Location = new Point(22, 49) });
        _summary = new Label { AutoSize = true, ForeColor = Color.FromArgb(58, 196, 187), Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(910, 31) };
        header.Controls.Add(_summary);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.FromArgb(7, 42, 78),
            BorderStyle = BorderStyle.None, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
        };
        AddColumn("Date source", 105); AddColumn("Banque", 170); AddColumn("Nom du compte", 180); AddColumn("Référence / IBAN", 210); AddColumn("Titulaire", 180); AddColumn("Type", 125);
        _grid.CellDoubleClick += (_, _) => EditSelected();

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = Color.FromArgb(4, 36, 73) };
        var close = MakeButton("Fermer", Color.FromArgb(51, 73, 99), 110); close.DialogResult = DialogResult.Cancel;
        var delete = MakeButton("Supprimer + données", Color.FromArgb(166, 61, 76), 165);
        var edit = MakeButton("Modifier", Color.FromArgb(8, 73, 137), 120);
        var add = MakeButton("Nouvelle source", Color.FromArgb(34, 149, 255), 145);
        var sort = MakeButton("Tri 3 champs", Color.FromArgb(16, 112, 187), 135);
        delete.Click += (_, _) => DeleteSelected(); edit.Click += (_, _) => EditSelected(); add.Click += (_, _) => EditAccount(null); sort.Click += (_, _) => ConfigureSort();
        footer.Controls.Add(close); footer.Controls.Add(delete); footer.Controls.Add(edit); footer.Controls.Add(add); footer.Controls.Add(sort);
        Controls.Add(_grid); Controls.Add(footer); Controls.Add(header); Shown += (_, _) => Reload();
    }

    private void AddColumn(string title, int width) => _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });

    private void ConfigureSort()
    {
        var fields = new[] { "Date source", "Banque", "Nom du compte", "Référence / IBAN", "Titulaire", "Type" };
        var selected = MultiColumnSortDialog.Select(this, fields, _sortCriteria);
        if (selected is null) return;
        _sortCriteria = selected;
        RenderAccounts();
    }

    private void Reload()
    {
        _accounts = BankingRepository.LoadConfiguration().Accounts;
        RenderAccounts();
    }

    private void RenderAccounts()
    {
        var selectors = new Dictionary<string, Func<BankAccountProfile, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date source"] = x => x.SourceDate, ["Banque"] = x => x.BankName, ["Nom du compte"] = x => x.AccountName,
            ["Référence / IBAN"] = x => x.AccountReference, ["Titulaire"] = x => x.Holder, ["Type"] = x => TypeText(x.Type)
        };
        _accounts = MultiColumnSorter.Apply(_accounts, _sortCriteria, selectors);
        _grid.Rows.Clear();
        foreach (var account in _accounts) _grid.Rows.Add(account.SourceDate?.ToString("dd/MM/yyyy") ?? "", account.BankName, account.AccountName, account.AccountReference, account.Holder, TypeText(account.Type));
        _summary.Text = $"{_accounts.Count} source(s)";
    }

    private BankAccountProfile? SelectedAccount()
    {
        if (_grid.CurrentRow is null) return null;
        var index = _grid.CurrentRow.Index;
        return index >= 0 && index < _accounts.Count ? _accounts[index] : null;
    }

    private void EditSelected() { var account = SelectedAccount(); if (account is not null) EditAccount(account); }

    private void EditAccount(BankAccountProfile? existing)
    {
        using var form = new SourceEditorForm(existing);
        if (form.ShowDialog(this) != DialogResult.OK || form.Account is null) return;
        var duplicate = SourceDataService.FindDuplicateSource(form.Account.BankName, form.Account.AccountName, form.Account.AccountReference, form.Account.SourceDate, existing?.Id);
        if (duplicate is not null)
        {
            MessageBox.Show($"La source « {duplicate.DisplayName} » existe déjà pour cette date.\n\nAucune nouvelle source n'a été créée.", "QNB - Source déjà existante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        BankingRepository.SaveAccount(form.Account); Reload();
    }

    private void DeleteSelected()
    {
        var account = SelectedAccount(); if (account is null) return;
        var usage = SourceDataService.GetUsage(account.Id);
        var message = usage.Imports > 0 || usage.Operations > 0
            ? $"Supprimer définitivement la source « {account.DisplayName} » ?\n\nCette action supprimera aussi :\n• {usage.Imports} import(s)\n• {usage.Operations} opération(s)\n\nCette suppression est irréversible."
            : $"Supprimer définitivement la source « {account.DisplayName} » ?";
        if (MessageBox.Show(message, "QNB - Suppression source et données", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        SourceDataService.DeleteSourceAndData(account.Id); Reload();
    }

    private static string TypeText(BankAccountType type) => type switch { BankAccountType.Courant => "Compte courant", BankAccountType.Epargne => "Épargne", BankAccountType.CarteDifferee => "Carte différée", _ => "Autre" };
    private static Button MakeButton(string text, Color backColor, int width) => new() { Text = text, Width = width, Height = 36, BackColor = backColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
}

internal sealed class SourceEditorForm : Form
{
    private readonly DateTimePicker _sourceDate = new();
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
        StartPosition = FormStartPosition.CenterParent; Size = new Size(600, 480); MinimumSize = new Size(600, 480);
        BackColor = Color.FromArgb(3, 23, 49); ForeColor = Color.White; Font = new Font("Segoe UI", 9.5F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 7, BackColor = BackColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sourceDate.Format = DateTimePickerFormat.Custom; _sourceDate.CustomFormat = "dd/MM/yyyy"; _sourceDate.Value = (existing?.SourceDate ?? DateTime.Today).Date;
        _bank.Text = existing?.BankName ?? string.Empty; _name.Text = existing?.AccountName ?? string.Empty; _reference.Text = existing?.AccountReference ?? string.Empty; _holder.Text = existing?.Holder ?? string.Empty;
        _type.DropDownStyle = ComboBoxStyle.DropDownList; _type.Items.AddRange(new object[] { "Compte courant", "Épargne", "Carte différée", "Autre" });
        _type.SelectedIndex = existing?.Type switch { BankAccountType.Epargne => 1, BankAccountType.CarteDifferee => 2, BankAccountType.Autre => 3, _ => 0 };
        AddRow(layout, 0, "Date de la source", _sourceDate); AddRow(layout, 1, "Banque", _bank); AddRow(layout, 2, "Nom du compte", _name); AddRow(layout, 3, "Référence / IBAN", _reference); AddRow(layout, 4, "Titulaire", _holder); AddRow(layout, 5, "Type de compte", _type);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0) };
        var save = new Button { Text = "Enregistrer", Width = 125, Height = 36, BackColor = Color.FromArgb(34, 149, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Annuler", Width = 110, Height = 36, BackColor = Color.FromArgb(51, 73, 99), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save(); buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 6); layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_bank.Text) || string.IsNullOrWhiteSpace(_name.Text)) { MessageBox.Show("Renseignez au minimum la banque et le nom du compte.", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        Account = new BankAccountProfile { Id = _id, SourceDate = _sourceDate.Value.Date, BankName = _bank.Text.Trim(), AccountName = _name.Text.Trim(), AccountReference = _reference.Text.Trim(), Holder = _holder.Text.Trim(), Type = _type.SelectedIndex switch { 1 => BankAccountType.Epargne, 2 => BankAccountType.CarteDifferee, 3 => BankAccountType.Autre, _ => BankAccountType.Courant } };
        DialogResult = DialogResult.OK; Close();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, Control control)
    {
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(183, 207, 229), TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
