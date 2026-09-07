using System.Globalization;

namespace QNB;

internal sealed class OperationsForm : Form
{
    private readonly DataGridView _grid;
    private readonly ComboBox _bankFilter;
    private readonly ComboBox _accountFilter;
    private readonly List<OperationRow> _allRows;

    public OperationsForm()
    {
        Text = "QNB - Opérations";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1320, 820);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.3F);

        _allRows = BankingRepository.LoadImports()
            .SelectMany(import => import.Operations.Select(operation => new OperationRow(import, operation)))
            .OrderByDescending(x => x.Date)
            .ToList();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label
        {
            Text = "OPÉRATIONS",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold)
        }, 0, 0);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.FromArgb(4, 36, 73),
            Padding = new Padding(12, 9, 12, 8)
        };

        _bankFilter = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _accountFilter = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
        _bankFilter.Items.Add("Toutes les banques");
        foreach (var bank in _allRows.Select(x => x.Bank).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
            _bankFilter.Items.Add(bank);
        _bankFilter.SelectedIndex = 0;
        _bankFilter.SelectedIndexChanged += (_, _) => RefreshAccountFilter();

        _accountFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        filters.Controls.Add(new Label { Text = "Banque", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(0, 7, 8, 0) });
        filters.Controls.Add(_bankFilter);
        filters.Controls.Add(new Label { Text = "Compte", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(18, 7, 8, 0) });
        filters.Controls.Add(_accountFilter);
        layout.Controls.Add(filters, 0, 1);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(4, 36, 73),
            BorderStyle = BorderStyle.None,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(25, 89, 145)
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(8, 73, 137);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(7, 43, 82);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 82, 146);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Banque", DataPropertyName = nameof(OperationRow.Bank), FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Compte", DataPropertyName = nameof(OperationRow.Account), FillWeight = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = nameof(OperationRow.Date), FillWeight = 65, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nature", DataPropertyName = nameof(OperationRow.Nature), FillWeight = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Débit", DataPropertyName = nameof(OperationRow.Debit), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Crédit", DataPropertyName = nameof(OperationRow.Credit), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Libellé", DataPropertyName = nameof(OperationRow.Label), FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Détails", DataPropertyName = nameof(OperationRow.Details), FillWeight = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Carte différée", DataPropertyName = nameof(OperationRow.DeferredCard), FillWeight = 75 });
        layout.Controls.Add(_grid, 0, 2);

        Controls.Add(layout);
        RefreshAccountFilter();
    }

    private void RefreshAccountFilter()
    {
        var selectedBank = _bankFilter.SelectedItem?.ToString();
        var rows = string.IsNullOrWhiteSpace(selectedBank) || selectedBank == "Toutes les banques"
            ? _allRows
            : _allRows.Where(x => x.Bank == selectedBank).ToList();

        _accountFilter.Items.Clear();
        _accountFilter.Items.Add("Tous les comptes");
        foreach (var account in rows.Select(x => x.Account).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
            _accountFilter.Items.Add(account);
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
        _grid.DataSource = rows.ToList();
    }

    private sealed class OperationRow
    {
        public string Bank { get; }
        public string Account { get; }
        public DateTime Date { get; }
        public string Nature { get; }
        public decimal Debit { get; }
        public decimal Credit { get; }
        public string Label { get; }
        public string Details { get; }
        public string DeferredCard { get; }

        public OperationRow(BankImportResult import, BankOperation operation)
        {
            Bank = string.IsNullOrWhiteSpace(import.BankName) ? "—" : import.BankName;
            Account = string.IsNullOrWhiteSpace(import.AccountDisplayName) ? import.AccountReference : import.AccountDisplayName;
            Date = operation.Date;
            Nature = operation.Nature;
            Debit = operation.Debit;
            Credit = operation.Credit;
            Label = operation.InterbankLabel;
            Details = operation.Details;
            DeferredCard = operation.IsDeferredCardSummary ? "Oui" : string.Empty;
        }
    }
}
