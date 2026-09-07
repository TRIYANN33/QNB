using System.Drawing.Drawing2D;
using System.Globalization;

namespace QNB;

public sealed class MainForm : Form
{
    private Label _dateTimeLabel = null!;
    private Label _documentsValue = null!;
    private Label _operationsValue = null!;
    private Label _accountsValue = null!;
    private Label _deferredCardsValue = null!;
    private readonly System.Windows.Forms.Timer _clockTimer;

    private static readonly Color Navy950 = Color.FromArgb(3, 23, 49);
    private static readonly Color Navy900 = Color.FromArgb(4, 36, 73);
    private static readonly Color Navy700 = Color.FromArgb(8, 73, 137);
    private static readonly Color Blue = Color.FromArgb(34, 149, 255);
    private static readonly Color Teal = Color.FromArgb(58, 196, 187);
    private static readonly Color Violet = Color.FromArgb(163, 132, 255);
    private static readonly Color Amber = Color.FromArgb(242, 187, 72);
    private static readonly Color Rose = Color.FromArgb(238, 118, 137);
    private static readonly Color Surface = Color.FromArgb(8, 43, 82);
    private static readonly Color Line = Color.FromArgb(49, 127, 190);
    private static readonly Color TextSoft = Color.FromArgb(183, 207, 229);

    public MainForm()
    {
        Text = "QNB - Tableau de bord";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1480, 930);
        Font = new Font("Segoe UI", 10F);
        BackColor = Navy950;
        ForeColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;

        var usageInfo = AppUsageInfo.LoadAndRegisterCurrentUse();
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10),
            BackColor = Navy950,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildDashboard(usageInfo), 1, 0);
        Controls.Add(root);

        RefreshDashboardStats();
        UpdateDateTime();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateDateTime();
        _clockTimer.Start();
        FormClosed += (_, _) => _clockTimer.Dispose();
    }

    private Control BuildSidebar()
    {
        var sidebar = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 28,
            BackColor = Navy900,
            BorderColor = Color.FromArgb(31, 115, 182),
            BorderWidth = 1,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(14, 14, 14, 12)
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        brand.Controls.Add(new PictureBox
        {
            Image = BrandAssets.CreateHummingbirdLogo(Color.White),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Location = new Point(5, 1),
            Size = new Size(56, 84)
        });
        brand.Controls.Add(new Label
        {
            Text = "QNB", AutoSize = true, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold), Location = new Point(69, 20)
        });
        brand.Controls.Add(new Label
        {
            Text = "PLUS LOIN ENSEMBLE", AutoSize = true, ForeColor = TextSoft,
            Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold), Location = new Point(71, 58)
        });

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 2)
        };

        var items = new (string Icon, string Text)[]
        {
            ("⌂", "Tableau de bord"), ("▣", "Patrimoine"), ("⇩", "Importation PDF / Excel / CSV"),
            ("◫", "Sources opérations"), ("≡", "Opérations"), ("◆", "Classification"),
            ("▤", "Référentiel"), ("●", "Couleurs"), ("☷", "Listes"), ("✓", "Contrôles"),
            ("⚙", "Règles auto"), ("◉", "Analyser les doublons"), ("↻", "Actualiser le classeur"),
            ("↻", "Actualiser feuille"), ("?", "Mode emploi")
        };
        for (var i = 0; i < items.Length; i++)
            menu.Controls.Add(CreateSidebarButton(items[i].Icon, items[i].Text, i == 0));

        var footer = new Label
        {
            Text = "PERFORMANCE\nCONFIANCE\nAVENIR",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(111, 176, 230),
            Font = new Font("Segoe UI", 7.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(9, 0, 0, 0)
        };

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private Control BuildDashboard(AppUsageInfo usageInfo)
    {
        var host = new StadiumPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Navy950, Padding = new Padding(10, 0, 0, 0) };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 4, 12, 12)
        };
        content.Controls.Add(BuildHero(), 0, 0);
        content.Controls.Add(BuildKpis(), 0, 1);
        content.Controls.Add(BuildInformationPanel(usageInfo), 0, 2);
        content.Controls.Add(BuildSectionLabel("Actions rapides"), 0, 3);
        content.Controls.Add(BuildQuickActions(), 0, 4);
        content.Controls.Add(BuildFooter(), 0, 5);
        host.Controls.Add(content);
        return host;
    }

    private Control BuildHero()
    {
        var hero = new RoundedPanel
        {
            Height = 190,
            Dock = DockStyle.Top,
            Radius = 26,
            BackColor = Navy900,
            BorderColor = Color.FromArgb(24, 103, 170),
            BorderWidth = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        hero.Paint += PaintHero;
        hero.Controls.Add(new PictureBox
        {
            Image = BrandAssets.CreateHummingbirdLogo(Color.White), SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent, Size = new Size(88, 132), Location = new Point(52, 28)
        });
        hero.Controls.Add(new Label
        {
            Text = "QNB", AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 46F, FontStyle.Bold), Location = new Point(151, 38)
        });
        hero.Controls.Add(new Label
        {
            Text = "PLUS LOIN ENSEMBLE", AutoSize = true, ForeColor = Color.FromArgb(224, 238, 250),
            BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), Location = new Point(158, 112)
        });
        hero.Controls.Add(new Label
        {
            Name = "heroSlogan", Text = "VOS DONNÉES\nNOTRE EXPERTISE\nVOTRE AVENIR", AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), Location = new Point(760, 74)
        });
        _dateTimeLabel = new Label
        {
            Size = new Size(260, 48), Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(209, 234, 252), BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Location = new Point(905, 16)
        };
        hero.Controls.Add(_dateTimeLabel);
        hero.Resize += (_, _) =>
        {
            _dateTimeLabel.Location = new Point(Math.Max(760, hero.ClientSize.Width - 282), 17);
            if (hero.Controls["heroSlogan"] is Control slogan)
                slogan.Location = new Point(Math.Max(610, hero.ClientSize.Width - 310), 74);
        };
        return hero;
    }

    private Control BuildKpis()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };
        for (var i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        grid.Controls.Add(CreateKpiCard("Documents", "0", "Fichiers importés", "▤", Blue, out _documentsValue), 0, 0);
        grid.Controls.Add(CreateKpiCard("Opérations", "0", "Lignes analysées", "≡", Teal, out _operationsValue), 1, 0);
        grid.Controls.Add(CreateKpiCard("Comptes", "0", "Banques & comptes", "▣", Amber, out _accountsValue), 2, 0);
        grid.Controls.Add(CreateKpiCard("Cartes différées", "0 €", "Débits globaux détectés", "◈", Violet, out _deferredCardsValue), 3, 0);
        grid.Controls.Add(CreateKpiCard("Doublons", "0", "Doublons trouvés", "◉", Rose, out _), 4, 0);
        return grid;
    }

    private Control BuildInformationPanel(AppUsageInfo usageInfo)
    {
        var card = new RoundedPanel
        {
            Height = 168, Dock = DockStyle.Top, Radius = 22, BackColor = Color.FromArgb(7, 42, 78),
            BorderColor = Line, BorderWidth = 1, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(22, 16, 22, 16)
        };
        card.Controls.Add(new Label
        {
            Text = "Informations de l'application", AutoSize = true, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold), Location = new Point(22, 15)
        });
        var brand = new Panel { BackColor = Color.Transparent, Location = new Point(22, 48), Size = new Size(300, 100) };
        brand.Controls.Add(new PictureBox
        {
            Image = BrandAssets.CreateHummingbirdLogo(Color.White), SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent, Location = new Point(2, 0), Size = new Size(58, 90)
        });
        brand.Controls.Add(new Label { Text = "QNB", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 30F, FontStyle.Bold), Location = new Point(67, 16) });
        brand.Controls.Add(new Label { Text = "PLUS LOIN ENSEMBLE", AutoSize = true, ForeColor = TextSoft, Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold), Location = new Point(70, 63) });
        card.Controls.Add(brand);

        var details = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, BackColor = Color.Transparent, Location = new Point(350, 48), Size = new Size(580, 104) };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        AddDetailRow(details, 0, "Date de création", FormatDateTime(usageInfo.CreationDate));
        AddDetailRow(details, 1, "Dernière utilisation", usageInfo.PreviousUseDate.HasValue ? FormatDateTime(usageInfo.PreviousUseDate.Value) : "Première utilisation");
        AddDetailRow(details, 2, "Dernière modification", FormatDateTime(GetApplicationLastModificationDate()));
        AddDetailRow(details, 3, "Version", "1.1.0  •  .NET 6");
        card.Controls.Add(details);

        var calendar = new RoundedPanel
        {
            Size = new Size(185, 108), Radius = 18, BackColor = Color.FromArgb(9, 57, 104),
            BorderColor = Color.FromArgb(30, 131, 211), BorderWidth = 1, Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(980, 42)
        };
        calendar.Controls.Add(new Label { Text = DateTime.Now.ToString("dddd", CultureInfo.GetCultureInfo("fr-FR")), Dock = DockStyle.Top, Height = 27, TextAlign = ContentAlignment.BottomCenter, ForeColor = Blue, Font = new Font("Segoe UI", 9F) });
        calendar.Controls.Add(new Label { Text = DateTime.Now.Day.ToString("00"), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Blue, Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold) });
        calendar.Controls.Add(new Label { Text = DateTime.Now.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")).ToUpperInvariant(), Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.TopCenter, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold) });
        card.Controls.Add(calendar);
        card.Resize += (_, _) =>
        {
            calendar.Location = new Point(Math.Max(955, card.ClientSize.Width - 205), 42);
            details.Width = Math.Max(490, calendar.Left - details.Left - 22);
        };
        return card;
    }

    private static void AddDetailRow(TableLayoutPanel grid, int row, string caption, string value)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        grid.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextSoft, Font = new Font("Segoe UI", 8.5F) }, 0, row);
        grid.Controls.Add(new Label { Text = ":  " + value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 8.7F, FontStyle.Bold) }, 1, row);
    }

    private Control BuildQuickActions()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 2, BackColor = Color.Transparent };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        var actions = new (string Icon, string Text, Color Color)[]
        {
            ("⇩", "Importer un relevé", Blue), ("≡", "Opérations", Navy700),
            ("▥", "Voir les résultats", Teal), ("▣", "Accéder au patrimoine", Color.FromArgb(129, 91, 27)),
            ("✓", "Lancer les contrôles", Navy700), ("◆", "Classification", Violet),
            ("◉", "Analyser les doublons", Rose), ("↻", "Actualiser le classeur", Navy700)
        };
        for (var i = 0; i < actions.Length; i++)
            grid.Controls.Add(CreateActionButton(actions[i].Icon, actions[i].Text, actions[i].Color), i % 4, i / 4);
        return grid;
    }

    private Button CreateSidebarButton(string icon, string text, bool selected)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}", Width = 210, Height = 34, Margin = new Padding(0, 1, 0, 1),
            FlatStyle = FlatStyle.Flat, BackColor = selected ? Color.FromArgb(23, 94, 164) : Navy900,
            ForeColor = Color.FromArgb(229, 241, 250), Font = new Font("Segoe UI Semibold", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, 82, 146);
        button.FlatAppearance.MouseDownBackColor = Blue;
        SetRoundedRegion(button, 12);
        button.Resize += (_, _) => SetRoundedRegion(button, 12);
        button.Click += (_, _) =>
        {
            if (text.StartsWith("Importation", StringComparison.OrdinalIgnoreCase)) ImportDocument();
            else if (string.Equals(text, "Opérations", StringComparison.OrdinalIgnoreCase)) ShowOperations();
            else MessageBox.Show($"Module « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        return button;
    }

    private Button CreateActionButton(string icon, string text, Color background)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}", Dock = DockStyle.Fill, Margin = new Padding(5), Padding = new Padding(10, 0, 8, 0),
            BackColor = background, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(background, 14);
        button.FlatAppearance.MouseDownBackColor = Darken(background, 14);
        SetRoundedRegion(button, 14);
        button.Resize += (_, _) => SetRoundedRegion(button, 14);
        button.Click += (_, _) =>
        {
            if (text.StartsWith("Importer", StringComparison.OrdinalIgnoreCase)) ImportDocument();
            else if (string.Equals(text, "Opérations", StringComparison.OrdinalIgnoreCase)) ShowOperations();
            else MessageBox.Show($"Action « {text} »", "QNB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        return button;
    }

    private void ImportDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Importer un relevé bancaire",
            Filter = "Relevés CSV (*.csv)|*.csv|Fichiers Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Documents PDF (*.pdf)|*.pdf|Tous les fichiers (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (extension != ".csv")
        {
            MessageBox.Show("Cette version prend en charge les relevés CSV. Le support PDF et Excel sera ajouté ensuite.", "QNB - Importation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var result = BankImportService.ImportCsv(dialog.FileName);

            using var accountForm = new BankAccountSelectionForm(result);
            if (accountForm.ShowDialog(this) != DialogResult.OK || accountForm.SelectedAccount is null)
                return;
            BankImportService.BindAccount(result, accountForm.SelectedAccount);

            using var preview = new ImportPreviewForm(result);
            if (preview.ShowDialog(this) != DialogResult.OK) return;

            BankImportService.SaveImport(result);
            RefreshDashboardStats();

            var deferredCount = result.Operations.Count(x => x.IsDeferredCardSummary);
            MessageBox.Show(
                $"Importation terminée avec succès.\n\nBanque : {result.BankName}\nCompte : {result.AccountDisplayName}\nOpérations : {result.Operations.Count}\nDébits carte différée détectés : {deferredCount}",
                "QNB - Importation réussie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Impossible d'importer ce fichier.\n\n" + ex.Message, "QNB - Erreur d'importation", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowOperations()
    {
        using var form = new OperationsForm();
        form.ShowDialog(this);
    }

    private void RefreshDashboardStats()
    {
        var stats = BankingRepository.GetDashboardStats();
        _documentsValue.Text = stats.Documents.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        _operationsValue.Text = stats.Operations.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        _accountsValue.Text = stats.Accounts.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        _deferredCardsValue.Text = stats.DeferredCardAmount.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " €";
    }

    private static Control CreateKpiCard(string title, string value, string subtitle, string icon, Color accent, out Label valueLabel)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Height = 112, Radius = 20, BackColor = Surface, BorderColor = Color.FromArgb(31, 113, 180), BorderWidth = 1, Margin = new Padding(5), Padding = new Padding(15) };
        var iconBox = new RoundedPanel { Radius = 14, BackColor = Darken(accent, 35), BorderColor = accent, BorderWidth = 1, Size = new Size(48, 48), Location = new Point(15, 14) };
        iconBox.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI Symbol", 17F, FontStyle.Bold) });
        card.Controls.Add(iconBox);
        card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = TextSoft, Font = new Font("Segoe UI", 8.3F), Location = new Point(72, 15) });
        valueLabel = new Label { Text = value, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold), Location = new Point(70, 34) };
        card.Controls.Add(valueLabel);
        card.Controls.Add(new Label { Text = subtitle, AutoSize = true, ForeColor = TextSoft, Font = new Font("Segoe UI", 7.5F), Location = new Point(17, 78) });
        card.Controls.Add(new RoundedPanel { Radius = 3, BackColor = accent, Size = new Size(80, 5), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Location = new Point(17, 96) });
        return card;
    }

    private static Control BuildSectionLabel(string text) => new Label
    {
        Text = text, AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent,
        Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), Margin = new Padding(4, 5, 0, 6)
    };

    private static Control BuildFooter() => new Label
    {
        Text = "QNB   |   Système de contrôle et d'analyse documentaire", Dock = DockStyle.Top, Height = 42,
        TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(139, 176, 207), BackColor = Color.Transparent,
        Font = new Font("Segoe UI", 8F), Margin = new Padding(6, 8, 0, 0)
    };

    private static void PaintHero(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = control.ClientRectangle;
        using var gradient = new LinearGradientBrush(rect, Color.FromArgb(5, 39, 78), Color.FromArgb(8, 92, 166), LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(gradient, rect);
        using var glow = new SolidBrush(Color.FromArgb(50, 92, 203, 255));
        e.Graphics.FillEllipse(glow, rect.Width - 360, -130, 420, 420);
        using var beamPen = new Pen(Color.FromArgb(65, 154, 217, 255), 1.3F);
        var points = new[] { new Point(rect.Width / 2 - 210, 9), new Point(rect.Width / 2 + 180, 7), new Point(rect.Width - 115, 18) };
        foreach (var p in points)
        {
            for (var i = -2; i <= 2; i++) e.Graphics.DrawLine(beamPen, p.X, p.Y, p.X + i * 80, rect.Height);
            using var lamp = new SolidBrush(Color.FromArgb(235, 236, 249, 255));
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    e.Graphics.FillEllipse(lamp, p.X + x * 7 - 3, p.Y + y * 7 - 3, 6, 6);
        }
    }

    private void UpdateDateTime()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        _dateTimeLabel.Text = DateTime.Now.ToString("dddd d MMMM yyyy\nHH:mm:ss", culture);
    }

    private static string FormatDateTime(DateTime dateTime) => dateTime.ToString("dd/MM/yyyy  HH:mm", CultureInfo.GetCultureInfo("fr-FR"));

    private static DateTime GetApplicationLastModificationDate()
    {
        try { return File.GetLastWriteTime(Application.ExecutablePath); }
        catch { return DateTime.Now; }
    }

    private static Color Lighten(Color color, int amount) => Color.FromArgb(Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));
    private static Color Darken(Color color, int amount) => Color.FromArgb(Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));

    private static void SetRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = CreateRoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region?.Dispose();
        control.Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(2, radius * 2);
        var arc = new Rectangle(rectangle.X, rectangle.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter; path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter; path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left; path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 18;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderWidth { get; set; }
        public RoundedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnResize(EventArgs e) { base.OnResize(e); SetRoundedRegion(this, Radius); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            base.OnPaint(e);
            if (BorderWidth <= 0 || BorderColor == Color.Transparent || Width < 4 || Height < 4) return;
            using var path = CreateRoundedPath(new Rectangle(1, 1, Width - 3, Height - 3), Math.Max(2, Radius - 1));
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class StadiumPanel : Panel
    {
        public StadiumPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using var gradient = new LinearGradientBrush(rect, Navy950, Color.FromArgb(4, 52, 101), LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(gradient, rect);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var glow = new SolidBrush(Color.FromArgb(22, 58, 163, 235));
            e.Graphics.FillEllipse(glow, rect.Width - 470, -120, 520, 520);
            using var pen = new Pen(Color.FromArgb(28, 120, 190, 245), 1F);
            for (var i = 0; i < 9; i++)
            {
                var x = 70 + i * Math.Max(80, rect.Width / 10);
                e.Graphics.DrawLine(pen, x, 0, rect.Width / 2, rect.Height);
            }
        }
    }
}
