using System.Globalization;

namespace QNB;

internal sealed class OperationsForm : Form
{
    private readonly DataGridView _grid;
    private readonly ComboBox _bankFilter;
    private readonly ComboBox _accountFilter;
    private readonly TextBox _searchBox;
    private readonly List<OperationRow> _allRows;
    private readonly Button _deleteButton;
    private readonly Label _countLabel;
    private readonly Label _totalLabel;
    private IReadOnlyList<MultiSortCriterion> _sortCriteria = Array.Empty<MultiSortCriterion>();

    public OperationsForm()
    {
        Text = "QNB - Opérations";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1320, 820);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.3F);

        var classifications = BankingRepository.LoadOperationClassifications();
        _allRows = BankingRepository.LoadImports()
            .SelectMany(import => import.Operations.Select(operation => new OperationRow(import, operation, classifications.TryGetValue(operation.Id, out var classification) ? classification : null)))
            .OrderByDescending(x => x.Date)
            .ToList();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4, BackColor = BackColor };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label { Text = "OPÉRATIONS", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold) }, 0, 0);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.FromArgb(4, 36, 73), Padding = new Padding(12, 9, 12, 8) };
        _bankFilter = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _accountFilter = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
        _bankFilter.Items.Add("Toutes les banques");
        foreach (var bank in _allRows.Select(x => x.Bank).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)) _bankFilter.Items.Add(bank);
        _bankFilter.SelectedIndex = 0;
        _bankFilter.SelectedIndexChanged += (_, _) => RefreshAccountFilter();
        _accountFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        filters.Controls.Add(new Label { Text = "Banque", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(0, 7, 8, 0) });
        filters.Controls.Add(_bankFilter);
        filters.Controls.Add(new Label { Text = "Compte", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(18, 7, 8, 0) });
        filters.Controls.Add(_accountFilter);
        filters.Controls.Add(new Label { Text = "Recherche", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(18, 7, 8, 0) });
        _searchBox = new TextBox { Width = 240, PlaceholderText = "Rechercher dans toutes les colonnes..." };
        _searchBox.TextChanged += (_, _) => ApplyFilters();
        filters.Controls.Add(_searchBox);
        var sortButton = new Button { Text = "Tri 3 champs", Width = 125, Height = 30, Margin = new Padding(18, 0, 0, 0), BackColor = Color.FromArgb(34, 149, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        sortButton.Click += (_, _) => ConfigureSort();
        filters.Controls.Add(sortButton);
        _deleteButton = new Button { Text = "✕  Supprimer cochées", Width = 180, Height = 34, Margin = new Padding(12, 0, 0, 0), BackColor = Color.FromArgb(190, 48, 58), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        _deleteButton.FlatAppearance.BorderColor = Color.FromArgb(245, 115, 120);
        _deleteButton.FlatAppearance.BorderSize = 1;
        _deleteButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 58, 68);
        _deleteButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(155, 35, 45);
        _deleteButton.Click += (_, _) => DeleteCheckedOperations();
        filters.Controls.Add(_deleteButton);
        var manualButton = new Button { Text = "Typage manuel", Width = 130, Height = 34, Margin = new Padding(12,0,0,0), BackColor = Color.FromArgb(108,76,170), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        manualButton.Click += (_, _) => ClassifySelectedManually(); filters.Controls.Add(manualButton);
        var autoButton = new Button { Text = "Typage auto", Width = 115, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(25,130,105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        autoButton.Click += (_, _) => ApplyAutomaticTyping(); filters.Controls.Add(autoButton);

        layout.Controls.Add(filters, 0, 1);

        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.FromArgb(7, 43, 82), Padding = new Padding(14, 5, 14, 5) };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        _countLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold) };
        _totalLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(58, 196, 187), Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold) };
        summary.Controls.Add(_countLabel, 0, 0);
        summary.Controls.Add(_totalLabel, 1, 0);
        layout.Controls.Add(summary, 0, 2);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(4, 36, 73), BorderStyle = BorderStyle.None, AutoGenerateColumns = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = false, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(25, 89, 145)
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(8, 73, 137);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(7, 43, 82);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 82, 146);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Suppr.", DataPropertyName = nameof(OperationRow.DeleteSelected), Width = 55, FillWeight = 42, ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Banque", DataPropertyName = nameof(OperationRow.Bank), FillWeight = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Compte", DataPropertyName = nameof(OperationRow.Account), FillWeight = 110, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = nameof(OperationRow.Date), FillWeight = 65, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = nameof(OperationRow.Type), FillWeight = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "S_Type", DataPropertyName = nameof(OperationRow.SubType), FillWeight = 95, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mode", DataPropertyName = nameof(OperationRow.ClassificationMode), FillWeight = 60, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nature", DataPropertyName = nameof(OperationRow.Nature), FillWeight = 140, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Débit", DataPropertyName = nameof(OperationRow.Debit), FillWeight = 70, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Crédit", DataPropertyName = nameof(OperationRow.Credit), FillWeight = 70, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Libellé", DataPropertyName = nameof(OperationRow.Label), FillWeight = 150, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Détails", DataPropertyName = nameof(OperationRow.Details), FillWeight = 180, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Carte différée", DataPropertyName = nameof(OperationRow.DeferredCard), FillWeight = 75, SortMode = DataGridViewColumnSortMode.NotSortable });
        foreach (DataGridViewColumn column in _grid.Columns)
            if (column is not DataGridViewCheckBoxColumn) column.ReadOnly = true;
        _grid.CellFormatting += FormatAmountCells;
        _grid.CellFormatting += FormatClassificationCells;
        layout.Controls.Add(_grid, 0, 3);

        Controls.Add(layout);
        RefreshAccountFilter();
    }

    private void FormatAmountCells(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Value is null) return;

        var propertyName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (propertyName != nameof(OperationRow.Debit) && propertyName != nameof(OperationRow.Credit)) return;

        if (!decimal.TryParse(Convert.ToString(e.Value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount == 0m)
        {
            // Montant nul : conserver le style normal du tableau.
            e.CellStyle.BackColor = _grid.DefaultCellStyle.BackColor;
            e.CellStyle.ForeColor = _grid.DefaultCellStyle.ForeColor;
            e.CellStyle.SelectionBackColor = _grid.DefaultCellStyle.SelectionBackColor;
            e.CellStyle.SelectionForeColor = _grid.DefaultCellStyle.SelectionForeColor;
            return;
        }

        if (propertyName == nameof(OperationRow.Debit))
        {
            e.CellStyle.BackColor = Color.FromArgb(118, 35, 45);
            e.CellStyle.ForeColor = Color.White;
            e.CellStyle.SelectionBackColor = Color.FromArgb(155, 45, 58);
            e.CellStyle.SelectionForeColor = Color.White;
        }
        else
        {
            e.CellStyle.BackColor = Color.FromArgb(25, 105, 70);
            e.CellStyle.ForeColor = Color.White;
            e.CellStyle.SelectionBackColor = Color.FromArgb(32, 135, 88);
            e.CellStyle.SelectionForeColor = Color.White;
        }
    }

    private void FormatClassificationCells(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not OperationRow row) return;
        if (_grid.Columns[e.ColumnIndex].DataPropertyName != nameof(OperationRow.Type)) return;
        var rule=BankingRepository.LoadClassificationRules().Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.CellColor))
            .OrderBy(x=>x.Priority).FirstOrDefault(x=>string.Equals(x.Type,row.Type,StringComparison.CurrentCultureIgnoreCase)&&string.Equals(x.SubType,row.SubType,StringComparison.CurrentCultureIgnoreCase));
        if(rule is null)return;
        try
        {
            var color=ColorTranslator.FromHtml(rule.CellColor);e.CellStyle.BackColor=color;
            e.CellStyle.ForeColor=(color.R*299+color.G*587+color.B*114)/1000>140?Color.Black:Color.White;
            e.CellStyle.SelectionBackColor=color;e.CellStyle.SelectionForeColor=e.CellStyle.ForeColor;
        } catch { }
    }

    private void ClassifySelectedManually()
    {
        if (_grid.CurrentRow?.DataBoundItem is not OperationRow row) { MessageBox.Show("Sélectionnez une opération.", "QNB - Typage", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new ManualClassificationForm(row.Type,row.SubType);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        BankingRepository.SetOperationClassification(row.Id,dialog.OperationType,dialog.OperationSubType,"Manuel");
        row.Type=dialog.OperationType; row.SubType=dialog.OperationSubType; row.ClassificationMode="Manuel"; ApplyFilters();
    }

    private void ApplyAutomaticTyping()
    {
        var changed=BankingRepository.ApplyAutomaticClassification();
        var classifications=BankingRepository.LoadOperationClassifications();
        foreach(var row in _allRows) if(classifications.TryGetValue(row.Id,out var x)){row.Type=x.Type;row.SubType=x.SubType;row.ClassificationMode=x.Mode;}
        ApplyFilters();
        MessageBox.Show($"{changed} opération(s) classée(s) automatiquement. Les modifications manuelles sont conservées.", "QNB - Typage automatique", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DeleteCheckedOperations()
    {
        _grid.EndEdit();
        var selected = _allRows.Where(x => x.DeleteSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Cochez au moins une opération à supprimer.", "QNB - Suppression", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var answer = MessageBox.Show($"Supprimer définitivement {selected.Count} opération(s) de la base de données ?\n\nCette action est irréversible.", "QNB - Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        BankingRepository.DeleteOperations(selected.Select(x => x.Id));
        foreach (var row in selected) _allRows.Remove(row);
        RefreshAccountFilter();
        MessageBox.Show($"{selected.Count} opération(s) supprimée(s).", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ConfigureSort()
    {
        var fields = new[] { "Banque", "Compte", "Date", "Type", "S_Type", "Mode", "Nature", "Débit", "Crédit", "Libellé", "Détails", "Carte différée" };
        var selected = MultiColumnSortDialog.Select(this, fields, _sortCriteria);
        if (selected is null) return;
        _sortCriteria = selected;
        ApplyFilters();
    }

    private void RefreshAccountFilter()
    {
        var selectedBank = _bankFilter.SelectedItem?.ToString();
        var rows = string.IsNullOrWhiteSpace(selectedBank) || selectedBank == "Toutes les banques" ? _allRows : _allRows.Where(x => x.Bank == selectedBank).ToList();
        _accountFilter.Items.Clear();
        _accountFilter.Items.Add("Tous les comptes");
        foreach (var account in rows.Select(x => x.Account).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)) _accountFilter.Items.Add(account);
        _accountFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var bank = _bankFilter.SelectedItem?.ToString();
        var account = _accountFilter.SelectedItem?.ToString();
        IEnumerable<OperationRow> rows = _allRows;
        if (!string.IsNullOrWhiteSpace(bank) && bank != "Toutes les banques") rows = rows.Where(x => x.Bank == bank);
        if (!string.IsNullOrWhiteSpace(account) && account != "Tous les comptes") rows = rows.Where(x => x.Account == account);

        var search = _searchBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchCulture = CultureInfo.GetCultureInfo("fr-FR");
            rows = rows.Where(x =>
                ContainsSearch(x.Bank, search) ||
                ContainsSearch(x.Account, search) ||
                ContainsSearch(x.Date.ToString("dd/MM/yyyy", searchCulture), search) ||
                ContainsSearch(x.Type, search) || ContainsSearch(x.SubType, search) || ContainsSearch(x.ClassificationMode, search) ||
                ContainsSearch(x.Nature, search) ||
                ContainsSearch(x.Debit.ToString("N2", searchCulture), search) ||
                ContainsSearch(x.Credit.ToString("N2", searchCulture), search) ||
                ContainsSearch(x.Label, search) ||
                ContainsSearch(x.Details, search) ||
                ContainsSearch(x.DeferredCard, search));
        }

        var selectors = new Dictionary<string, Func<OperationRow, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Banque"] = x => x.Bank, ["Compte"] = x => x.Account, ["Date"] = x => x.Date, ["Type"] = x => x.Type, ["S_Type"] = x => x.SubType, ["Mode"] = x => x.ClassificationMode, ["Nature"] = x => x.Nature,
            ["Débit"] = x => x.Debit, ["Crédit"] = x => x.Credit, ["Libellé"] = x => x.Label, ["Détails"] = x => x.Details, ["Carte différée"] = x => x.DeferredCard
        };
        var displayedRows = MultiColumnSorter.Apply(rows, _sortCriteria, selectors).ToList();
        _grid.DataSource = displayedRows;

        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var total = displayedRows.Sum(x => x.Credit + x.Debit);
        _countLabel.Text = $"Opérations saisies : {_allRows.Count:N0}   •   Affichées : {displayedRows.Count:N0}";
        _totalLabel.Text = $"Total affiché : {total.ToString("N2", culture)} €";
    }

    private static bool ContainsSearch(string? value, string search) =>
        !string.IsNullOrEmpty(value) && value.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private sealed class OperationRow
    {
        public long Id { get; }
        public bool DeleteSelected { get; set; }
        public string Bank { get; }
        public string Account { get; }
        public DateTime Date { get; }
        public string Type { get; set; }
        public string SubType { get; set; }
        public string ClassificationMode { get; set; }
        public string Nature { get; }
        public decimal Debit { get; }
        public decimal Credit { get; }
        public string Label { get; }
        public string Details { get; }
        public string DeferredCard { get; }

        public OperationRow(BankImportResult import, BankOperation operation, OperationClassification? classification)
        {
            Id = operation.Id;
            Type=classification?.Type??string.Empty; SubType=classification?.SubType??string.Empty; ClassificationMode=classification?.Mode??string.Empty;
            Bank = string.IsNullOrWhiteSpace(import.BankName) ? "—" : import.BankName;
            Account = string.IsNullOrWhiteSpace(import.AccountDisplayName) ? import.AccountReference : import.AccountDisplayName;
            Date = operation.Date; Nature = operation.Nature; Debit = operation.Debit; Credit = operation.Credit;
            Label = operation.InterbankLabel; Details = operation.Details; DeferredCard = operation.IsDeferredCardSummary ? "Oui" : string.Empty;
        }
    }
}
