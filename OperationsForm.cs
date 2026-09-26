using System.Globalization;

namespace QNB;

internal sealed class OperationsForm : Form
{
    private readonly DataGridView _grid;
    private readonly ComboBox _bankFilter;
    private readonly ComboBox _accountFilter;
    private readonly TextBox _searchBox;
    private readonly HashSet<string> _checkedBanks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checkedAccounts = new(StringComparer.OrdinalIgnoreCase);
    private bool _showCheckedBankAccounts;
    private bool _showCheckedOperations;
    private readonly DateTimePicker _periodFrom;
    private readonly DateTimePicker _periodTo;
    private readonly CheckBox _periodEnabled;
    private readonly CheckBox _withoutTypeFilter;
    private readonly List<OperationRow> _allRows;
    private readonly Button _deleteButton;
    private readonly Label _countLabel;
    private readonly Label _totalLabel;
    private readonly TextBox _selectedOperationInfo;
    private readonly Label _checkedSelectionInfo;
    private readonly NumericUpDown _goToNumber;
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = BackColor };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.Controls.Add(new Label { Text = "OPÉRATIONS", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold) }, 0, 0);
        var selectedPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.Transparent,Margin=new Padding(8,4,0,4)};
        selectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent,58F));selectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent,42F));
        _selectedOperationInfo = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(7,43,82), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10F), PlaceholderText = "Sélectionnez une opération : Date • Libellé • Détail" };
        _checkedSelectionInfo=new Label{Dock=DockStyle.Fill,Text="Saisie sélectionnée : 0 opération",TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(242,187,72),Font=new Font("Segoe UI Semibold",9F,FontStyle.Bold)};
        selectedPanel.Controls.Add(_selectedOperationInfo,0,0);selectedPanel.Controls.Add(_checkedSelectionInfo,0,1);header.Controls.Add(selectedPanel,1,0);
        layout.Controls.Add(header, 0, 0);

        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(4,36,73), Padding = new Padding(12,8,12,8) };
        filters.RowStyles.Add(new RowStyle(SizeType.Percent,33.33F)); filters.RowStyles.Add(new RowStyle(SizeType.Percent,33.33F)); filters.RowStyles.Add(new RowStyle(SizeType.Percent,33.34F));
        var filterRow = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.LeftToRight, WrapContents=false, BackColor=Color.Transparent, AutoScroll=true };
        var navigationRow = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.LeftToRight, WrapContents=false, BackColor=Color.Transparent, AutoScroll=true };
        var actionRow = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.LeftToRight, WrapContents=false, BackColor=Color.Transparent, AutoScroll=true };
        _bankFilter = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _accountFilter = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
        _bankFilter.Items.Add("Toutes les banques");
        foreach (var bank in _allRows.Select(x => x.Bank).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)) _bankFilter.Items.Add(bank);
        _bankFilter.SelectedIndex = 0;
        _bankFilter.SelectedIndexChanged += (_, _) => RefreshAccountFilter();
        _accountFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        filterRow.Controls.Add(new Label { Text = "Banque", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(0, 7, 8, 0) });
        filterRow.Controls.Add(_bankFilter);
        filterRow.Controls.Add(new Label { Text = "Compte", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(18, 7, 8, 0) });
        filterRow.Controls.Add(_accountFilter);
        var chooseBanks = new Button { Text = "☑ Banques...", Width = 115, Height = 30, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        chooseBanks.Click += (_, _) => ChooseValues("Banques à afficher", _allRows.Select(x => x.Bank), _checkedBanks);
        filterRow.Controls.Add(chooseBanks);
        var chooseAccounts = new Button { Text = "☑ Comptes...", Width = 115, Height = 30, Margin = new Padding(5,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        chooseAccounts.Click += (_, _) => ChooseValues("Comptes à afficher", _allRows.Select(x => x.Account), _checkedAccounts);
        filterRow.Controls.Add(chooseAccounts);
        filterRow.Controls.Add(new Label { Text = "Recherche", AutoSize = true, ForeColor = Color.FromArgb(183, 207, 229), Margin = new Padding(18, 7, 8, 0) });
        _searchBox = new TextBox { Width = 240, PlaceholderText = "Rechercher dans toutes les colonnes..." };
        _searchBox.TextChanged += (_, _) => ApplyFilters();
        filterRow.Controls.Add(_searchBox);
        _periodEnabled = new CheckBox { Text = "Période", AutoSize = true, ForeColor = Color.FromArgb(183,207,229), Margin = new Padding(18,6,5,0) };
        var minDate=_allRows.Count>0?_allRows.Min(x=>x.Date).Date:DateTime.Today;
        var maxDate=_allRows.Count>0?_allRows.Max(x=>x.Date).Date:DateTime.Today;
        _periodFrom = new DateTimePicker { Width=115, Format=DateTimePickerFormat.Short, Value=minDate, Enabled=false };
        _periodTo = new DateTimePicker { Width=115, Format=DateTimePickerFormat.Short, Value=maxDate, Enabled=false };
        _periodEnabled.CheckedChanged += (_,_)=>{_periodFrom.Enabled=_periodEnabled.Checked;_periodTo.Enabled=_periodEnabled.Checked;ApplyFilters();};
        _periodFrom.ValueChanged += (_,_)=>ApplyFilters(); _periodTo.ValueChanged += (_,_)=>ApplyFilters();
        navigationRow.Controls.Add(_periodEnabled);
        navigationRow.Controls.Add(new Label { Text="Du",AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(5,7,4,0) }); navigationRow.Controls.Add(_periodFrom);
        navigationRow.Controls.Add(new Label { Text="au",AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(5,7,4,0) }); navigationRow.Controls.Add(_periodTo);
        _withoutTypeFilter = new CheckBox { Text = "Sans Type", AutoSize = true, ForeColor = Color.FromArgb(255,205,120), Margin = new Padding(18,6,5,0) };
        _withoutTypeFilter.CheckedChanged += (_,_) => ApplyFilters();
        navigationRow.Controls.Add(_withoutTypeFilter);
        filterRow.Controls.Add(new Label { Text = "N°", AutoSize = true, ForeColor = Color.FromArgb(183,207,229), Margin = new Padding(18,7,4,0), Visible = false });
        _goToNumber = new NumericUpDown { Width = 75, Minimum = 1, Maximum = Math.Max(1,_allRows.Count), Margin = new Padding(0,2,0,0) };
        var goButton = new Button { Text = "Aller", Width = 58, Height = 30, Margin = new Padding(4,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        goButton.Click += (_,_) => GoToNumber(); _goToNumber.KeyDown += (_,e)=>{if(e.KeyCode==Keys.Enter){GoToNumber();e.SuppressKeyPress=true;}};
        navigationRow.Controls.Add(new Label { Text = "Navigation", AutoSize = true, ForeColor = Color.FromArgb(255,205,120), Font = new Font("Segoe UI Semibold",9.3F,FontStyle.Bold), Margin = new Padding(18,7,8,0) });
        navigationRow.Controls.Add(new Label { Text = "N°", AutoSize = true, ForeColor = Color.FromArgb(183,207,229), Margin = new Padding(0,7,4,0) });
        navigationRow.Controls.Add(_goToNumber); navigationRow.Controls.Add(goButton);
        var sortButton = new Button { Text = "Tri 3 champs", Width = 125, Height = 30, Margin = new Padding(18, 0, 0, 0), BackColor = Color.FromArgb(34, 149, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        sortButton.Click += (_, _) => ConfigureSort();
        navigationRow.Controls.Add(sortButton);
        var addButton = new Button { Text = "＋ Ajouter", Width = 105, Height = 34, Margin = new Padding(0,0,0,0), BackColor = Color.FromArgb(25,130,105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        addButton.Click += (_, _) => AddManualOperation(); actionRow.Controls.Add(addButton);
        var duplicateButton = new Button { Text = "Dupliquer ligne", Width = 125, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        duplicateButton.Click += (_, _) => DuplicateCurrentOperation(); actionRow.Controls.Add(duplicateButton);
        _deleteButton = new Button { Text = "✕  Supprimer cochées", Width = 180, Height = 34, Margin = new Padding(12, 0, 0, 0), BackColor = Color.FromArgb(190, 48, 58), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        _deleteButton.FlatAppearance.BorderColor = Color.FromArgb(245, 115, 120);
        _deleteButton.FlatAppearance.BorderSize = 1;
        _deleteButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 58, 68);
        _deleteButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(155, 35, 45);
        _deleteButton.Click += (_, _) => DeleteCheckedOperations();
        actionRow.Controls.Add(_deleteButton);
        var showBankAccounts = new Button { Text = "Afficher banques/comptes cochés", Width = 225, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        showBankAccounts.Click += (_, _) => { _showCheckedBankAccounts = true; ApplyFilters(); };
        actionRow.Controls.Add(showBankAccounts);
        var showOperations = new Button { Text = "Afficher opérations cochées", Width = 205, Height = 34, Margin = new Padding(5,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        showOperations.Click += (_, _) => { _grid.EndEdit(); _showCheckedOperations = true; ApplyFilters(); };
        actionRow.Controls.Add(showOperations);
        var showAll = new Button { Text = "Tout afficher", Width = 105, Height = 34, Margin = new Padding(5,0,0,0), BackColor = Color.FromArgb(16,112,187), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        showAll.Click += (_, _) => { _showCheckedBankAccounts = false; _showCheckedOperations = false; _bankFilter.SelectedIndex = 0; _searchBox.Clear(); _periodEnabled.Checked = false; _withoutTypeFilter.Checked = false; RefreshAccountFilter(); };
        actionRow.Controls.Add(showAll);
        var manualButton = new Button { Text = "Typage manuel", Width = 130, Height = 34, Margin = new Padding(12,0,0,0), BackColor = Color.FromArgb(108,76,170), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        manualButton.Click += (_, _) => ClassifySelectedManually(); actionRow.Controls.Add(manualButton);
        var autoButton = new Button { Text = "Typage auto", Width = 115, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(25,130,105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        autoButton.Click += (_, _) => ApplyAutomaticTyping(); actionRow.Controls.Add(autoButton);
        var ruleButton = new Button { Text = "Règle → cochées", Width = 145, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(185,120,35), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        ruleButton.Click += (_, _) => ApplyRuleToCheckedOperations(); actionRow.Controls.Add(ruleButton);
        var checkAllButton = new Button { Text = "Tout cocher", Width = 105, Height = 34, Margin = new Padding(8,0,0,0), BackColor = Color.FromArgb(45,105,165), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        checkAllButton.Click += (_, _) => SetDisplayedChecks(true); actionRow.Controls.Add(checkAllButton);
        var uncheckAllButton = new Button { Text = "Tout décocher", Width = 115, Height = 34, Margin = new Padding(5,0,0,0), BackColor = Color.FromArgb(70,82,100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        uncheckAllButton.Click += (_, _) => SetDisplayedChecks(false); actionRow.Controls.Add(uncheckAllButton);

        filters.Controls.Add(filterRow,0,0); filters.Controls.Add(navigationRow,0,1); filters.Controls.Add(actionRow,0,2);
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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "N°", DataPropertyName = nameof(OperationRow.Number), Width = 55, FillWeight = 42, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Choix", DataPropertyName = nameof(OperationRow.DeleteSelected), Width = 55, FillWeight = 42, ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Afficher", DataPropertyName = nameof(OperationRow.DisplaySelected), Width = 65, FillWeight = 48, ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable });
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
        _grid.SelectionChanged += (_, _) => UpdateSelectedOperationInfo();
        _grid.CellClick += (_, e) =>
        {
            UpdateSelectedOperationInfo();
            if(e.RowIndex<0 || e.ColumnIndex<0) return;
            if(_grid.Columns[e.ColumnIndex].DataPropertyName!=nameof(OperationRow.Label)) return;
            if(_grid.Rows[e.RowIndex].DataBoundItem is not OperationRow row) return;
            _grid.EndEdit();
            row.DeleteSelected=!row.DeleteSelected;
            _grid.InvalidateRow(e.RowIndex);
            UpdateCheckedSelectionInfo();
        };
        layout.Controls.Add(_grid, 0, 3);

        Controls.Add(layout);
        RefreshAccountFilter();
    }


    private void AddManualOperation()
    {
        var imports=BankingRepository.LoadImports();
        if(imports.Count==0){MessageBox.Show("Aucun compte bancaire disponible. Importez d'abord un compte.","QNB - Opérations",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var dialog=new ManualOperationForm(imports,null);
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        BankingRepository.AddManualOperation(dialog.SelectedImport,dialog.Operation);
        ReloadOperations();
    }

    private void DuplicateCurrentOperation()
    {
        if(_grid.CurrentRow?.DataBoundItem is not OperationRow row){MessageBox.Show("Sélectionnez la ligne à dupliquer.","QNB - Opérations",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var imports=BankingRepository.LoadImports();
        var source=imports.FirstOrDefault(i=>i.Operations.Any(o=>o.Id==row.Id));
        var operation=source?.Operations.FirstOrDefault(o=>o.Id==row.Id);
        if(source is null||operation is null)return;
        using var dialog=new ManualOperationForm(imports,(source,operation));
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        BankingRepository.AddManualOperation(dialog.SelectedImport,dialog.Operation);
        ReloadOperations();
    }

    private void ReloadOperations()
    {
        var classifications=BankingRepository.LoadOperationClassifications();
        _allRows.Clear();
        _allRows.AddRange(BankingRepository.LoadImports().SelectMany(import=>import.Operations.Select(operation=>new OperationRow(import,operation,classifications.TryGetValue(operation.Id,out var classification)?classification:null))).OrderByDescending(x=>x.Date));
        RefreshAccountFilter();
    }

    private void GoToNumber()
    {
        var number=(int)_goToNumber.Value;
        foreach(DataGridViewRow gridRow in _grid.Rows)
        {
            if(gridRow.DataBoundItem is OperationRow row && row.Number==number)
            {
                _grid.ClearSelection();gridRow.Selected=true;_grid.CurrentCell=gridRow.Cells.Cast<DataGridViewCell>().FirstOrDefault(x=>x.Visible);
                _grid.FirstDisplayedScrollingRowIndex=gridRow.Index;UpdateSelectedOperationInfo();return;
            }
        }
        MessageBox.Show($"Le numéro {number} n'est pas présent dans la sélection actuelle.","QNB - Opérations",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private void UpdateCheckedSelectionInfo()
    {
        var selected=_allRows.Where(x=>x.DeleteSelected).ToList();
        var debit=selected.Sum(x=>Math.Abs(x.Debit));
        var credit=selected.Sum(x=>Math.Abs(x.Credit));
        _checkedSelectionInfo.Text=selected.Count==0?"Saisie sélectionnée : 0 opération":$"Saisie sélectionnée : {selected.Count} opération(s)   •   Débit {debit:N2} €   •   Crédit {credit:N2} €";
    }

    private void UpdateSelectedOperationInfo()
    {
        if (_grid.CurrentRow?.DataBoundItem is not OperationRow row)
        {
            _selectedOperationInfo.Text = string.Empty;
            return;
        }
        var label = string.IsNullOrWhiteSpace(row.Label) ? row.Nature : row.Label;
        var details = string.IsNullOrWhiteSpace(row.Details) ? "—" : row.Details;
        _selectedOperationInfo.Text = $"{row.Date:dd/MM/yyyy}   •   {label}   •   {details}";
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

    private void SetDisplayedChecks(bool value)
    {
        _grid.EndEdit();
        foreach(DataGridViewRow gridRow in _grid.Rows)
            if(gridRow.DataBoundItem is OperationRow row) row.DeleteSelected=value;
        _grid.Refresh();
        UpdateCheckedSelectionInfo();
    }

    private void ApplyRuleToCheckedOperations()
    {
        _grid.EndEdit();
        var selected=_allRows.Where(x=>x.DeleteSelected).ToList();
        if(selected.Count==0){MessageBox.Show("Cochez une ou plusieurs opérations.","QNB - Règle auto",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var rules=BankingRepository.LoadClassificationRules().Where(x=>x.Enabled).OrderBy(x=>x.Priority).ThenBy(x=>x.Type).ThenBy(x=>x.SubType).ToList();
        if(rules.Count==0){MessageBox.Show("Aucune règle automatique active.","QNB - Règle auto",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var dialog=new Form{Text="Choisir une règle automatique",StartPosition=FormStartPosition.CenterParent,Size=new Size(620,180),MinimumSize=new Size(520,180),BackColor=Color.FromArgb(3,23,49),ForeColor=Color.White,Font=new Font("Segoe UI",10F)};
        var combo=new ComboBox{Left=20,Top=25,Width=560,DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=27,DisplayMember="Text"};
        var choices=rules.Select(x=>new RuleChoice(x,$"{x.Type} / {x.SubType}")).ToList();combo.DataSource=choices;
        combo.DrawItem+=(s,e)=>{
            if(e.Index<0||e.Index>=choices.Count)return;
            e.DrawBackground();var item=choices[e.Index];var back=e.BackColor;var fore=e.ForeColor;
            if(!string.IsNullOrWhiteSpace(item.Rule.CellColor))try{back=ColorTranslator.FromHtml(item.Rule.CellColor);fore=(back.R*299+back.G*587+back.B*114)/1000>140?Color.Black:Color.White;}catch{}
            using var brush=new SolidBrush(back);e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,item.Text,e.Font,e.Bounds,fore,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();
        };
        var validate=new Button{Text=$"Valider sur {selected.Count} opération(s)",Left=365,Top=75,Width=215,Height=34,BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.OK};
        var cancel=new Button{Text="Annuler",Left=255,Top=75,Width=100,Height=34,BackColor=Color.FromArgb(70,82,100),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};
        dialog.Controls.Add(combo);dialog.Controls.Add(validate);dialog.Controls.Add(cancel);dialog.AcceptButton=validate;dialog.CancelButton=cancel;
        if(dialog.ShowDialog(this)!=DialogResult.OK||combo.SelectedItem is not RuleChoice choice)return;
        foreach(var row in selected){BankingRepository.SetOperationClassification(row.Id,choice.Rule.Type,choice.Rule.SubType,"Règle");row.Type=choice.Rule.Type;row.SubType=choice.Rule.SubType;row.ClassificationMode="Règle";row.DeleteSelected=false;}
        UpdateCheckedSelectionInfo();ApplyFilters();MessageBox.Show($"{selected.Count} opération(s) typée(s) avec la règle « {choice.Rule.Type} / {choice.Rule.SubType} ».","QNB - Règle auto",MessageBoxButtons.OK,MessageBoxIcon.Information);
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

    private void ChooseValues(string title, IEnumerable<string> values, HashSet<string> selected)
    {
        using var dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = new Size(440, 520), MinimumSize = new Size(350, 350), BackColor = Color.FromArgb(3,23,49), ForeColor = Color.White };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BackColor = Color.FromArgb(7,43,82), ForeColor = Color.White, BorderStyle = BorderStyle.None };
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            list.Items.Add(value, selected.Contains(value));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "Valider", Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", Width = 90, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        dialog.Controls.Add(list); dialog.Controls.Add(buttons); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        selected.Clear();
        foreach (var item in list.CheckedItems) selected.Add(item.ToString()!);
        if (_showCheckedBankAccounts) ApplyFilters();
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
        if (_showCheckedBankAccounts)
        {
            if (_checkedBanks.Count > 0) rows = rows.Where(x => _checkedBanks.Contains(x.Bank));
            if (_checkedAccounts.Count > 0) rows = rows.Where(x => _checkedAccounts.Contains(x.Account));
        }
        if (_showCheckedOperations) rows = rows.Where(x => x.DisplaySelected);
        if (_withoutTypeFilter.Checked) rows = rows.Where(x => string.IsNullOrWhiteSpace(x.Type));
        if (_periodEnabled.Checked)
        {
            var from=_periodFrom.Value.Date; var to=_periodTo.Value.Date;
            if(from>to)(from,to)=(to,from);
            rows=rows.Where(x=>x.Date.Date>=from&&x.Date.Date<=to);
        }

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
        for(var i=0;i<displayedRows.Count;i++)displayedRows[i].Number=i+1;
        _grid.DataSource = null; _grid.DataSource = displayedRows;
        _goToNumber.Maximum=Math.Max(1,displayedRows.Count);if(_goToNumber.Value>_goToNumber.Maximum)_goToNumber.Value=_goToNumber.Maximum;

        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var total = displayedRows.Sum(x => x.Credit + x.Debit);
        _countLabel.Text = $"Opérations saisies : {_allRows.Count:N0}   •   Affichées : {displayedRows.Count:N0}";
        _totalLabel.Text = $"Total affiché : {total.ToString("N2", culture)} €";
    }

    private static bool ContainsSearch(string? value, string search) =>
        !string.IsNullOrEmpty(value) && value.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private sealed record RuleChoice(ClassificationRule Rule,string Text);

    private sealed class OperationRow
    {
        public long Id { get; }
        public int Number { get; set; }
        public bool DeleteSelected { get; set; }
        public bool DisplaySelected { get; set; }
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
