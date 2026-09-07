using System.Globalization;

namespace QNB;

internal sealed class ImportPreviewForm : Form
{
    private static readonly Color Navy950 = Color.FromArgb(3, 23, 49);
    private static readonly Color Navy900 = Color.FromArgb(4, 36, 73);
    private static readonly Color Navy700 = Color.FromArgb(8, 73, 137);
    private static readonly Color Blue = Color.FromArgb(34, 149, 255);
    private static readonly Color TextSoft = Color.FromArgb(183, 207, 229);

    public ImportPreviewForm(BankImportResult result)
    {
        Text = "QNB - Aperçu de l'importation";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1040, 650);
        Size = new Size(1220, 760);
        BackColor = Navy950;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = Navy950
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

        var title = new Label
        {
            Text = "IMPORTATION BANCAIRE",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            BackColor = Navy900,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 12)
        };
        for (var i = 0; i < 4; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        summary.Controls.Add(CreateSummary("Banque", result.BankName), 0, 0);
        summary.Controls.Add(CreateSummary("Compte", MaskAccount(result.AccountReference)), 1, 0);
        summary.Controls.Add(CreateSummary("Solde", result.Balance.HasValue ? result.Balance.Value.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " " + result.Currency : "—"), 2, 0);
        summary.Controls.Add(CreateSummary("Opérations", result.Operations.Count.ToString(CultureInfo.InvariantCulture)), 3, 0);
        layout.Controls.Add(summary, 0, 1);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Navy900,
            BorderStyle = BorderStyle.None,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            GridColor = Color.FromArgb(25, 89, 145),
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 32 },
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Navy700;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Color.FromArgb(7, 43, 82);
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 82, 146);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = nameof(BankOperation.Date), FillWeight = 65, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nature", DataPropertyName = nameof(BankOperation.Nature), FillWeight = 190 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Débit", DataPropertyName = nameof(BankOperation.Debit), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Crédit", DataPropertyName = nameof(BankOperation.Credit), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Libellé", DataPropertyName = nameof(BankOperation.InterbankLabel), FillWeight = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Détails", DataPropertyName = nameof(BankOperation.Details), FillWeight = 180 });
        grid.DataSource = result.Operations;
        layout.Controls.Add(grid, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Navy950,
            Padding = new Padding(0, 10, 0, 0)
        };
        var importButton = CreateButton("Importer", Blue);
        importButton.DialogResult = DialogResult.OK;
        var cancelButton = CreateButton("Annuler", Color.FromArgb(51, 73, 99));
        cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(importButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 3);

        AcceptButton = importButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    private static Control CreateSummary(string caption, string value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Navy900, Padding = new Padding(4) };
        panel.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        });
        panel.Controls.Add(new Label
        {
            Text = caption.ToUpperInvariant(),
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold)
        });
        return panel;
    }

    private static Button CreateButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            Width = 120,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
    }

    private static string MaskAccount(string account)
    {
        var compact = account.Replace(" ", string.Empty);
        if (compact.Length <= 8) return account;
        return compact[..4] + " •••• •••• " + compact[^4..];
    }
}
