using System.Globalization;

namespace QNB;

public sealed class MainForm : Form
{
    private readonly Label _dateTimeLabel;
    private readonly System.Windows.Forms.Timer _clockTimer;

    private static readonly Color Background = Color.FromArgb(244, 247, 248);
    private static readonly Color Card = Color.FromArgb(255, 255, 255);
    private static readonly Color Primary = Color.FromArgb(69, 105, 108);
    private static readonly Color PrimaryDark = Color.FromArgb(48, 76, 79);
    private static readonly Color TextMain = Color.FromArgb(49, 61, 64);
    private static readonly Color TextSoft = Color.FromArgb(105, 119, 121);
    private static readonly Color SoftBlue = Color.FromArgb(222, 235, 238);
    private static readonly Color SoftGreen = Color.FromArgb(224, 238, 231);
    private static readonly Color SoftSand = Color.FromArgb(242, 235, 220);
    private static readonly Color SoftRose = Color.FromArgb(241, 226, 228);
    private static readonly Color SoftLavender = Color.FromArgb(232, 228, 241);

    public MainForm()
    {
        Text = "QNB - Écran principal";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 760);
        Size = new Size(1280, 850);
        Font = new Font("Segoe UI", 10F);
        BackColor = Background;
        ForeColor = TextMain;
        AutoScaleMode = AutoScaleMode.Dpi;

        var usageInfo = AppUsageInfo.LoadAndRegisterCurrentUse();

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 28, 34, 30),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background,
            AutoScroll = true
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 22),
            BackColor = Background
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

        var titlePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Background,
            Margin = Padding.Empty
        };
        titlePanel.Controls.Add(new Label
        {
            Text = "QNB",
            AutoSize = true,
            ForeColor = PrimaryDark,
            Font = new Font("Segoe UI Semibold", 32F, FontStyle.Bold),
            Margin = Padding.Empty
        });
        titlePanel.Controls.Add(new Label
        {
            Text = "Votre espace de gestion",
            AutoSize = true,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 12F),
            Margin = new Padding(3, 3, 0, 0)
        });

        _dateTimeLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Primary,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 0)
        };

        header.Controls.Add(titlePanel, 0, 0);
        header.Controls.Add(_dateTimeLabel, 1, 0);

        var informationCard = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Card,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 26)
        };
        for (var i = 0; i < 3; i++)
            informationCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

        informationCard.Controls.Add(CreateInfoTile("CRÉATION", FormatDateTime(usageInfo.CreationDate), SoftGreen), 0, 0);
        informationCard.Controls.Add(CreateInfoTile(
            "DERNIÈRE UTILISATION",
            usageInfo.PreviousUseDate.HasValue ? FormatDateTime(usageInfo.PreviousUseDate.Value) : "Première utilisation",
            SoftBlue), 1, 0);
        informationCard.Controls.Add(CreateInfoTile("DERNIÈRE MODIFICATION", FormatDateTime(GetApplicationLastModificationDate()), SoftSand), 2, 0);

        var sectionTitle = new Label
        {
            Text = "Fonctions principales",
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            Margin = new Padding(2, 0, 0, 14)
        };

        var buttonGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            Margin = Padding.Empty,
            BackColor = Background,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        for (var column = 0; column < 3; column++)
            buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        for (var row = 0; row < 5; row++)
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));

        var definitions = new (string Text, string Name, Color Color)[]
        {
            ("Tableau de bord", "btnDashboard", SoftBlue),
            ("Importation PDF ou Excel", "btnImportDocument", SoftGreen),
            ("Sources opérations", "btnOperationSources", SoftSand),
            ("Opérations", "btnOperations", SoftBlue),
            ("Classification", "btnClassification", SoftLavender),
            ("Référentiel", "btnRepository", SoftGreen),
            ("Couleurs", "btnColors", SoftRose),
            ("Listes", "btnLists", SoftSand),
            ("Mode emploi", "btnHelp", SoftBlue),
            ("Contrôles", "btnControls", SoftGreen),
            ("Règles auto", "btnAutomaticRules", SoftLavender),
            ("Analyser les doublons", "btnDuplicates", SoftRose),
            ("Actualiser le classeur", "btnRefreshWorkbook", SoftBlue),
            ("Actualiser feuille", "btnRefreshSheet", SoftGreen)
        };

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            buttonGrid.Controls.Add(CreateMainButton(definition.Text, definition.Name, definition.Color), index % 3, index / 3);
        }

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(informationCard, 0, 1);
        mainLayout.Controls.Add(sectionTitle, 0, 2);
        mainLayout.Controls.Add(buttonGrid, 0, 3);
        Controls.Add(mainLayout);

        UpdateDateTime();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateDateTime();
        _clockTimer.Start();
        FormClosed += (_, _) => _clockTimer.Dispose();
    }

    private static Panel CreateInfoTile(string title, string value, Color accent)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 92,
            BackColor = accent,
            Margin = new Padding(6),
            Padding = new Padding(18, 13, 18, 12)
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static Button CreateMainButton(string text, string name, Color backgroundColor)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(7),
            Padding = new Padding(12, 0, 12, 0),
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = TextMain,
            BackColor = backgroundColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(backgroundColor, 10);
        button.FlatAppearance.MouseDownBackColor = Darken(backgroundColor, 8);

        button.Click += (_, _) => MessageBox.Show(
            $"Le module « {text} » sera développé à l'étape suivante.",
            "QNB",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return button;
    }

    private static Color Lighten(Color color, int amount) => Color.FromArgb(
        Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));

    private static Color Darken(Color color, int amount) => Color.FromArgb(
        Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));

    private void UpdateDateTime()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        _dateTimeLabel.Text = DateTime.Now.ToString("dddd d MMMM yyyy\nHH:mm", culture);
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        return dateTime.ToString("d MMMM yyyy · HH:mm", culture);
    }

    private static DateTime GetApplicationLastModificationDate()
    {
        try { return File.GetLastWriteTime(Application.ExecutablePath); }
        catch { return DateTime.Now; }
    }
}
