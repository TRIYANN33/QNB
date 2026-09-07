using System.Globalization;

namespace QNB;

public sealed class MainForm : Form
{
    private readonly Label _dateTimeLabel;
    private readonly Panel _contentPanel;
    private readonly System.Windows.Forms.Timer _clockTimer;

    private static readonly Color Background = Color.FromArgb(244, 247, 248);
    private static readonly Color Card = Color.FromArgb(255, 255, 255);
    private static readonly Color Sidebar = Color.FromArgb(41, 58, 61);
    private static readonly Color SidebarHover = Color.FromArgb(56, 76, 79);
    private static readonly Color Primary = Color.FromArgb(79, 111, 113);
    private static readonly Color Accent = Color.FromArgb(145, 175, 164);
    private static readonly Color TextMain = Color.FromArgb(47, 58, 61);
    private static readonly Color TextSoft = Color.FromArgb(108, 121, 123);
    private static readonly Color SoftBlue = Color.FromArgb(224, 235, 239);
    private static readonly Color SoftGreen = Color.FromArgb(226, 239, 232);
    private static readonly Color SoftSand = Color.FromArgb(243, 237, 222);
    private static readonly Color SoftRose = Color.FromArgb(243, 229, 231);

    public MainForm()
    {
        Text = "QNB - Tableau de bord";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1380, 900);
        Font = new Font("Segoe UI", 10F);
        BackColor = Background;
        ForeColor = TextMain;
        AutoScaleMode = AutoScaleMode.Dpi;

        var usageInfo = AppUsageInfo.LoadAndRegisterCurrentUse();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var sidebar = BuildSidebar();
        _contentPanel = BuildDashboardContent(usageInfo);

        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(_contentPanel, 1, 0);
        Controls.Add(root);

        UpdateDateTime();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateDateTime();
        _clockTimer.Start();
        FormClosed += (_, _) => _clockTimer.Dispose();
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(16, 22, 16, 18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Sidebar
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brand = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            Margin = new Padding(0, 0, 0, 18)
        };
        brand.Controls.Add(new Label
        {
            Text = "QNB",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 28F, FontStyle.Bold),
            Location = new Point(6, 2)
        });
        brand.Controls.Add(new Label
        {
            Text = "Gestion & analyse",
            AutoSize = true,
            ForeColor = Color.FromArgb(181, 199, 195),
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(9, 52)
        });

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Sidebar,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var items = new (string Icon, string Text)[]
        {
            ("⌂", "Tableau de bord"),
            ("⇩", "Importation PDF / Excel"),
            ("◫", "Sources opérations"),
            ("≡", "Opérations"),
            ("◆", "Classification"),
            ("▤", "Référentiel"),
            ("●", "Couleurs"),
            ("☷", "Listes"),
            ("✓", "Contrôles"),
            ("⚙", "Règles auto"),
            ("◉", "Doublons"),
            ("?", "Mode emploi")
        };

        foreach (var item in items)
            menu.Controls.Add(CreateSidebarButton(item.Icon, item.Text));

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = Sidebar
        };
        footer.Controls.Add(new Label
        {
            Text = "QNB • .NET 6",
            ForeColor = Color.FromArgb(157, 178, 174),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(8, 20)
        });

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private Panel BuildDashboardContent(AppUsageInfo usageInfo)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            AutoScroll = true,
            Padding = new Padding(32, 28, 32, 28)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Background,
            Margin = Padding.Empty
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 24),
            BackColor = Background
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

        var titleBlock = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Background,
            Margin = Padding.Empty
        };
        titleBlock.Controls.Add(new Label
        {
            Text = "Tableau de bord",
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
            Margin = Padding.Empty
        });
        titleBlock.Controls.Add(new Label
        {
            Text = "Vue d’ensemble de votre activité QNB",
            AutoSize = true,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(2, 5, 0, 0)
        });

        _dateTimeLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Primary,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Margin = Padding.Empty
        };

        header.Controls.Add(titleBlock, 0, 0);
        header.Controls.Add(_dateTimeLabel, 1, 0);

        var kpiGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 26),
            BackColor = Background
        };
        for (var i = 0; i < 4; i++)
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        kpiGrid.Controls.Add(CreateKpiCard("Documents", "0", "Importés", "⇩", SoftBlue), 0, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Opérations", "0", "Enregistrées", "≡", SoftGreen), 1, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Contrôles", "0", "À vérifier", "✓", SoftSand), 2, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Doublons", "0", "Détectés", "◉", SoftRose), 3, 0);

        var infoTitle = CreateSectionTitle("Informations de l'application");
        var infoGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 26),
            BackColor = Background
        };
        for (var i = 0; i < 3; i++)
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

        infoGrid.Controls.Add(CreateInfoCard("Date de création", FormatDateTime(usageInfo.CreationDate)), 0, 0);
        infoGrid.Controls.Add(CreateInfoCard("Dernière utilisation",
            usageInfo.PreviousUseDate.HasValue ? FormatDateTime(usageInfo.PreviousUseDate.Value) : "Première utilisation"), 1, 0);
        infoGrid.Controls.Add(CreateInfoCard("Dernière modification", FormatDateTime(GetApplicationLastModificationDate())), 2, 0);

        var actionTitle = CreateSectionTitle("Actions rapides");
        var actionGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = Background
        };
        for (var i = 0; i < 3; i++)
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));

        var actions = new (string Icon, string Text, Color Color)[]
        {
            ("⇩", "Importer un document", SoftBlue),
            ("≡", "Ouvrir les opérations", SoftGreen),
            ("◆", "Classification", SoftSand),
            ("✓", "Lancer les contrôles", SoftGreen),
            ("◉", "Analyser les doublons", SoftRose),
            ("↻", "Actualiser le classeur", SoftBlue)
        };
        for (var i = 0; i < actions.Length; i++)
            actionGrid.Controls.Add(CreateActionButton(actions[i].Icon, actions[i].Text, actions[i].Color), i % 3, i / 3);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(kpiGrid, 0, 1);
        layout.Controls.Add(infoTitle, 0, 2);
        layout.Controls.Add(infoGrid, 0, 3);
        layout.Controls.Add(actionTitle, 0, 4);
        layout.Controls.Add(actionGrid, 0, 5);

        host.Controls.Add(layout);
        return host;
    }

    private static Button CreateSidebarButton(string icon, string text)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}",
            Width = 205,
            Height = 44,
            Margin = new Padding(0, 2, 0, 2),
            Padding = new Padding(2, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = Color.FromArgb(224, 233, 231),
            Font = new Font("Segoe UI Semibold", 9.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = SidebarHover;
        button.FlatAppearance.MouseDownBackColor = Primary;
        button.Click += (_, _) => MessageBox.Show($"Module « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return button;
    }

    private static Panel CreateKpiCard(string title, string value, string subtitle, string icon, Color accent)
    {
        var card = new Panel
        {
            Height = 118,
            Dock = DockStyle.Fill,
            BackColor = Card,
            Margin = new Padding(6),
            Padding = new Padding(18)
        };

        var iconBox = new Label
        {
            Text = icon,
            BackColor = accent,
            ForeColor = Primary,
            Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(48, 48),
            Location = new Point(18, 18)
        };
        var titleLabel = new Label
        {
            Text = title,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(78, 18)
        };
        var valueLabel = new Label
        {
            Text = value,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(76, 36)
        };
        var subtitleLabel = new Label
        {
            Text = subtitle,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Location = new Point(20, 86)
        };

        card.Controls.Add(iconBox);
        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(subtitleLabel);
        return card;
    }

    private static Panel CreateInfoCard(string title, string value)
    {
        var card = new Panel
        {
            Height = 86,
            Dock = DockStyle.Fill,
            BackColor = Card,
            Margin = new Padding(6),
            Padding = new Padding(18, 14, 18, 12)
        };
        card.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Label
        {
            Text = title.ToUpperInvariant(),
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold)
        });
        return card;
    }

    private static Button CreateActionButton(string icon, string text, Color background)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}",
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Padding = new Padding(14, 0, 12, 0),
            BackColor = background,
            ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(background, 8);
        button.FlatAppearance.MouseDownBackColor = Darken(background, 8);
        button.Click += (_, _) => MessageBox.Show($"Action « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return button;
    }

    private static Label CreateSectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = TextMain,
        Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
        Margin = new Padding(4, 0, 0, 10)
    };

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

    private static Color Lighten(Color color, int amount) => Color.FromArgb(
        Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));

    private static Color Darken(Color color, int amount) => Color.FromArgb(
        Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));
}
