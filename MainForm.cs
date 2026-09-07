namespace QNB;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "QNB";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1200, 750);

        var title = new Label
        {
            Text = "QNB",
            AutoSize = true,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            Location = new Point(30, 25)
        };

        var subtitle = new Label
        {
            Text = "Application C# / .NET",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F),
            Location = new Point(34, 75)
        };

        Controls.Add(title);
        Controls.Add(subtitle);
    }
}
