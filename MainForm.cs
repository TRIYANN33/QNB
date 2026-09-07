using System.Globalization;

namespace QNB;

public sealed class MainForm : Form
{
    private readonly Label _dateTimeLabel;
    private readonly Label _creationDateLabel;
    private readonly Label _lastUseDateLabel;
    private readonly Label _lastModificationDateLabel;
    private readonly System.Windows.Forms.Timer _clockTimer;

    public MainForm()
    {
        Text = "QNB - Écran principal";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 720);
        Size = new Size(1250, 820);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var usageInfo = AppUsageInfo.LoadAndRegisterCurrentUse();

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "QNB",
            AutoSize = true,
            Font = new Font("Segoe UI", 30F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var subtitle = new Label
        {
            Text = "Écran principal",
            AutoSize = true,
            Font = new Font("Segoe UI", 13F, FontStyle.Regular),
            Margin = new Padding(2, 0, 0, 18)
        };

        var headerPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };
        headerPanel.Controls.Add(title);
        headerPanel.Controls.Add(subtitle);

        var informationPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 24),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        informationPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
        informationPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _dateTimeLabel = CreateInformationValueLabel();
        _creationDateLabel = CreateInformationValueLabel(FormatDateTime(usageInfo.CreationDate));
        _lastUseDateLabel = CreateInformationValueLabel(
            usageInfo.PreviousUseDate.HasValue
                ? FormatDateTime(usageInfo.PreviousUseDate.Value)
                : "Première utilisation");
        _lastModificationDateLabel = CreateInformationValueLabel(FormatDateTime(GetApplicationLastModificationDate()));

        AddInformationRow(informationPanel, 0, "Date du jour et heure", _dateTimeLabel);
        AddInformationRow(informationPanel, 1, "Date de création", _creationDateLabel);
        AddInformationRow(informationPanel, 2, "Dernière utilisation", _lastUseDateLabel);
        AddInformationRow(informationPanel, 3, "Dernière modification", _lastModificationDateLabel);

        var sectionTitle = new Label
        {
            Text = "Fonctions",
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var buttonGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            Margin = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        for (var column = 0; column < 3; column++)
            buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

        for (var row = 0; row < 5; row++)
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));

        var buttonDefinitions = new (string Text, string Name)[]
        {
            ("Tableau de bord", "btnDashboard"),
            ("Importation document PDF ou Excel", "btnImportDocument"),
            ("Sources opérations", "btnOperationSources"),
            ("Opérations", "btnOperations"),
            ("Classification", "btnClassification"),
            ("Référentiel", "btnRepository"),
            ("Couleurs", "btnColors"),
            ("Listes", "btnLists"),
            ("Mode emploi", "btnHelp"),
            ("Contrôles", "btnControls"),
            ("Règles auto", "btnAutomaticRules"),
            ("Analyser les doublons", "btnDuplicates"),
            ("Actualiser le classeur", "btnRefreshWorkbook"),
            ("Actualiser feuille", "btnRefreshSheet")
        };

        for (var index = 0; index < buttonDefinitions.Length; index++)
        {
            var definition = buttonDefinitions[index];
            var button = CreateMainButton(definition.Text, definition.Name);
            buttonGrid.Controls.Add(button, index % 3, index / 3);
        }

        mainLayout.Controls.Add(headerPanel, 0, 0);
        mainLayout.Controls.Add(informationPanel, 0, 1);
        mainLayout.Controls.Add(sectionTitle, 0, 2);
        mainLayout.Controls.Add(buttonGrid, 0, 3);

        Controls.Add(mainLayout);

        UpdateDateTime();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateDateTime();
        _clockTimer.Start();

        FormClosed += (_, _) => _clockTimer.Dispose();
    }

    private static Label CreateInformationValueLabel(string text = "")
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 9, 10, 9)
        };
    }

    private static void AddInformationRow(TableLayoutPanel panel, int row, string caption, Label value)
    {
        var captionLabel = new Label
        {
            Text = caption,
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 9, 10, 9)
        };

        panel.Controls.Add(captionLabel, 0, row);
        panel.Controls.Add(value, 1, row);
    }

    private static Button CreateMainButton(string text, string name)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(7),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = true
        };

        button.Click += (_, _) => MessageBox.Show(
            $"Le module « {text} » sera développé à l'étape suivante.",
            "QNB",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        return button;
    }

    private void UpdateDateTime()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        _dateTimeLabel.Text = DateTime.Now.ToString("dddd d MMMM yyyy 'à' HH:mm", culture);
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        return dateTime.ToString("dddd d MMMM yyyy 'à' HH:mm", culture);
    }

    private static DateTime GetApplicationLastModificationDate()
    {
        try
        {
            var executablePath = Application.ExecutablePath;
            return File.GetLastWriteTime(executablePath);
        }
        catch
        {
            return DateTime.Now;
        }
    }
}
