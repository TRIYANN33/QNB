using System.Drawing.Drawing2D;
using System.Globalization;

namespace QNB;

public sealed class MainForm : Form
{
    private Label _dateTimeLabel = null!;
    private readonly Panel _contentPanel;
    private readonly System.Windows.Forms.Timer _clockTimer;

    private static readonly Color Background = Color.FromArgb(244, 247, 250);
    private static readonly Color Card = Color.White;
    private static readonly Color Navy = Color.FromArgb(8, 55, 96);
    private static readonly Color NavyDark = Color.FromArgb(5, 41, 75);
    private static readonly Color NavyHover = Color.FromArgb(18, 76, 124);
    private static readonly Color Sky = Color.FromArgb(105, 180, 232);
    private static readonly Color SkySoft = Color.FromArgb(226, 241, 251);
    private static readonly Color TextMain = Color.FromArgb(32, 48, 64);
    private static readonly Color TextSoft = Color.FromArgb(105, 118, 132);
    private static readonly Color Border = Color.FromArgb(222, 229, 235);
    private static readonly Color SoftGreen = Color.FromArgb(231, 243, 237);
    private static readonly Color SoftSand = Color.FromArgb(247, 240, 225);
    private static readonly Color SoftRose = Color.FromArgb(247, 233, 236);

    public MainForm()
    {
        Text = "QNB - Tableau de bord";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1400, 900);
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
            BackColor = Background,
            Padding = new Padding(10)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232F));
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
            BackColor = NavyDark,
            Margin = new Padding(4, 4, 8, 4),
            Padding = new Padding(14, 18, 14, 14)
        };
        ApplyRoundedCorners(sidebar, 24);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = NavyDark,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = NavyDark };
        brand.Controls.Add(new Label
        {
            Text = "QNB",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
            Location = new Point(5, 0)
        });
        brand.Controls.Add(new Label
        {
            Text = "GESTION & ANALYSE",
            AutoSize = true,
            ForeColor = Color.FromArgb(167, 202, 226),
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            Location = new Point(8, 45)
        });

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = NavyDark,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };

        var items = new (string Icon, string Text)[]
        {
            ("⌂", "Tableau de bord"),
            ("▣", "Patrimoine"),
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

        var footer = new Label
        {
            Text = "QNB  •  .NET 6",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(142, 181, 208),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8F),
            Padding = new Padding(7, 0, 0, 0)
        };

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
            Padding = new Padding(22, 4, 8, 4),
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Background,
            Margin = Padding.Empty
        };

        var hero = new HeroPanel
        {
            Dock = DockStyle.Top,
            Height = 132,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(24, 18, 24, 16),
            BackColor = Card
        };
        ApplyRoundedCorners(hero, 24);

        hero.Controls.Add(new Label
        {
            Text = "QNB",
            AutoSize = true,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Location = new Point(26, 18)
        });
        hero.Controls.Add(new Label
        {
            Text = "TABLEAU DE BORD",
            AutoSize = true,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
            Location = new Point(24, 48)
        });
        hero.Controls.Add(new Label
        {
            Text = "Vue synthétique de votre activité et de vos données",
            AutoSize = true,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(27, 91)
        });

        _dateTimeLabel = new Label
        {
            AutoSize = false,
            Size = new Size(260, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            Location = new Point(Math.Max(500, hero.Width - 290), 24)
        };
        hero.Controls.Add(_dateTimeLabel);
        hero.Resize += (_, _) => _dateTimeLabel.Location = new Point(Math.Max(500, hero.ClientSize.Width - 286), 25);

        var kpiGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Background
        };
        for (var i = 0; i < 4; i++) kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        kpiGrid.Controls.Add(CreateKpiCard("Documents", "0", "Importés", "⇩", SkySoft), 0, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Opérations", "0", "Enregistrées", "≡", SoftGreen), 1, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Contrôles", "0", "À vérifier", "✓", SoftSand), 2, 0);
        kpiGrid.Controls.Add(CreateKpiCard("Doublons", "0", "Détectés", "◉", SoftRose), 3, 0);

        var information = CreateInformationPanel(usageInfo);

        var actionTitle = CreateSectionTitle("Actions rapides");
        var actionGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = Background
        };
        for (var i = 0; i < 4; i++) actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));

        var actions = new (string Icon, string Text, Color Color)[]
        {
            ("▣", "Patrimoine", SkySoft),
            ("⇩", "Importer document", SkySoft),
            ("≡", "Opérations", SoftGreen),
            ("◆", "Classification", SoftSand),
            ("✓", "Contrôles", SoftGreen),
            ("◉", "Doublons", SoftRose),
            ("↻", "Actualiser classeur", SkySoft),
            ("↻", "Actualiser feuille", SoftGreen)
        };
        for (var i = 0; i < actions.Length; i++)
            actionGrid.Controls.Add(CreateActionButton(actions[i].Icon, actions[i].Text, actions[i].Color), i % 4, i / 4);

        layout.Controls.Add(hero, 0, 0);
        layout.Controls.Add(kpiGrid, 0, 1);
        layout.Controls.Add(information, 0, 2);
        layout.Controls.Add(actionTitle, 0, 3);
        layout.Controls.Add(actionGrid, 0, 4);
        host.Controls.Add(layout);
        return host;
    }

    private Panel CreateInformationPanel(AppUsageInfo usageInfo)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 116,
            BackColor = Card,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(20, 14, 20, 14)
        };
        ApplyRoundedCorners(card, 20);

        var title = new Label
        {
            Text = "Informations de l'application",
            Dock = DockStyle.Top,
            Height = 27,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold)
        };
        card.Controls.Add(title);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Card,
            Padding = new Padding(0, 5, 0, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));

        grid.Controls.Add(CreateInfoBlock("DATE DE CRÉATION", FormatDateTime(usageInfo.CreationDate)), 0, 0);
        grid.Controls.Add(CreateInfoBlock("DERNIÈRE UTILISATION",
            usageInfo.PreviousUseDate.HasValue ? FormatDateTime(usageInfo.PreviousUseDate.Value) : "Première utilisation"), 1, 0);
        grid.Controls.Add(CreateInfoBlock("DERNIÈRE MODIFICATION", FormatDateTime(GetApplicationLastModificationDate())), 2, 0);
        card.Controls.Add(grid);
        grid.BringToFront();
        return card;
    }

    private static Panel CreateInfoBlock(string title, string value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Card, Margin = new Padding(0, 0, 12, 0) };
        panel.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 21,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold)
        });
        return panel;
    }

    private static Button CreateSidebarButton(string icon, string text)
    {
        var button = new Button
        {
            Text = $"  {icon}   {text}",
            Width = 198,
            Height = 34,
            Margin = new Padding(0, 1, 0, 1),
            Padding = new Padding(3, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = NavyDark,
            ForeColor = Color.FromArgb(232, 241, 247),
            Font = new Font("Segoe UI Semibold", 8.8F),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = NavyHover;
        button.FlatAppearance.MouseDownBackColor = Sky;
        ApplyRoundedCorners(button, 12);
        button.Click += (_, _) => MessageBox.Show($"Module « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return button;
    }

    private static Panel CreateKpiCard(string title, string value, string subtitle, string icon, Color accent)
    {
        var card = new Panel
        {
            Height = 96,
            Dock = DockStyle.Fill,
            BackColor = Card,
            Margin = new Padding(4),
            Padding = new Padding(14)
        };
        ApplyRoundedCorners(card, 18);

        var iconBox = new Label
        {
            Text = icon,
            BackColor = accent,
            ForeColor = Navy,
            Font = new Font("Segoe UI Symbol", 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(40, 40),
            Location = new Point(14, 14)
        };
        ApplyRoundedCorners(iconBox, 12);

        card.Controls.Add(iconBox);
        card.Controls.Add(new Label
        {
            Text = title,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Location = new Point(66, 13)
        });
        card.Controls.Add(new Label
        {
            Text = value,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(64, 28)
        });
        card.Controls.Add(new Label
        {
            Text = subtitle,
            ForeColor = TextSoft,
            Font = new Font("Segoe UI", 8F),
            AutoSize = true,
            Location = new Point(15, 68)
        });
        return card;
    }

    private static Button CreateActionButton(string icon, string text, Color background)
    {
        var button = new Button
        {
            Text = $" {icon}   {text}",
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(10, 0, 8, 0),
            BackColor = background,
            ForeColor = Navy,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(background, 8);
        button.FlatAppearance.MouseDownBackColor = Darken(background, 8);
        ApplyRoundedCorners(button, 14);
        button.Click += (_, _) => MessageBox.Show($"Action « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return button;
    }

    private static Label CreateSectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Navy,
        Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
        Margin = new Padding(4, 0, 0, 7)
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

    private static void ApplyRoundedCorners(Control control, int radius)
    {
        void UpdateRegion()
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using var path = CreateRoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region?.Dispose();
            control.Region = new Region(path);
        }

        control.Resize += (_, _) => UpdateRegion();
        control.HandleCreated += (_, _) => UpdateRegion();
        UpdateRegion();
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Lighten(Color color, int amount) => Color.FromArgb(
        Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));

    private static Color Darken(Color color, int amount) => Color.FromArgb(
        Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));

    private sealed class HeroPanel : Panel
    {
        public HeroPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var whiteBrush = new SolidBrush(Color.White);
            e.Graphics.FillRectangle(whiteBrush, ClientRectangle);

            var blueArea = new Point[]
            {
                new(Math.Max(520, Width * 55 / 100), 0),
                new(Width, 0),
                new(Width, Height),
                new(Math.Max(650, Width * 64 / 100), Height)
            };
            using var navyBrush = new LinearGradientBrush(
                new Rectangle(Math.Max(1, Width / 2), 0, Math.Max(1, Width / 2), Math.Max(1, Height)),
                Sky, Navy, LinearGradientMode.Horizontal);
            e.Graphics.FillPolygon(navyBrush, blueArea);

            using var translucent = new SolidBrush(Color.FromArgb(45, Color.White));
            e.Graphics.FillPolygon(translucent, new[]
            {
                new Point(Math.Max(600, Width * 62 / 100), 0),
                new Point(Math.Max(690, Width * 71 / 100), Height / 2),
                new Point(Math.Max(610, Width * 63 / 100), Height),
                new Point(Math.Max(520, Width * 55 / 100), Height / 2)
            });

            base.OnPaint(e);
        }
    }
}
