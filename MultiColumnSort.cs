using System.Collections;
using System.Globalization;

namespace QNB;

internal sealed record MultiSortCriterion(string Field, bool Ascending);

internal static class MultiColumnSorter
{
    public static List<T> Apply<T>(IEnumerable<T> source, IReadOnlyList<MultiSortCriterion> criteria, IReadOnlyDictionary<string, Func<T, object?>> selectors)
    {
        var list = source.ToList();
        if (criteria.Count == 0) return list;
        list.Sort((left, right) =>
        {
            foreach (var criterion in criteria)
            {
                if (!selectors.TryGetValue(criterion.Field, out var selector)) continue;
                var comparison = CompareValues(selector(left), selector(right));
                if (comparison != 0) return criterion.Ascending ? comparison : -comparison;
            }
            return 0;
        });
        return list;
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        if (left is string ls && right is string rs)
            return CultureInfo.GetCultureInfo("fr-FR").CompareInfo.Compare(ls, rs, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

        if (left is IComparable comparable && left.GetType() == right.GetType())
            return comparable.CompareTo(right);

        return StringComparer.CurrentCultureIgnoreCase.Compare(Convert.ToString(left, CultureInfo.CurrentCulture), Convert.ToString(right, CultureInfo.CurrentCulture));
    }
}

internal sealed class MultiColumnSortDialog : Form
{
    private readonly ComboBox[] _fields = new ComboBox[3];
    private readonly ComboBox[] _directions = new ComboBox[3];
    public IReadOnlyList<MultiSortCriterion> Criteria { get; private set; } = Array.Empty<MultiSortCriterion>();

    private MultiColumnSortDialog(IReadOnlyList<string> fields, IReadOnlyList<MultiSortCriterion>? current)
    {
        Text = "QNB - Tri sur 3 champs";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(590, 270);
        MinimumSize = new Size(590, 270);
        BackColor = Color.FromArgb(3, 23, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 3, RowCount = 5, BackColor = BackColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        for (var i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Priorité", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(183, 207, 229) }, 0, 0);
        layout.Controls.Add(new Label { Text = "Champ", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(183, 207, 229) }, 1, 0);
        layout.Controls.Add(new Label { Text = "Ordre", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(183, 207, 229) }, 2, 0);

        for (var i = 0; i < 3; i++)
        {
            layout.Controls.Add(new Label { Text = (i + 1).ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, i + 1);
            _fields[i] = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _fields[i].Items.Add(i == 0 ? "Sélectionner un champ" : "(Aucun)");
            foreach (var field in fields) _fields[i].Items.Add(field);
            _fields[i].SelectedIndex = 0;
            _directions[i] = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _directions[i].Items.AddRange(new object[] { "A → Z", "Z → A" });
            _directions[i].SelectedIndex = 0;
            layout.Controls.Add(_fields[i], 1, i + 1);
            layout.Controls.Add(_directions[i], 2, i + 1);
        }

        if (current is not null)
        {
            for (var i = 0; i < Math.Min(3, current.Count); i++)
            {
                var index = _fields[i].Items.IndexOf(current[i].Field);
                if (index >= 0) _fields[i].SelectedIndex = index;
                _directions[i].SelectedIndex = current[i].Ascending ? 0 : 1;
            }
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
        var apply = new Button { Text = "Appliquer", Width = 120, Height = 34, BackColor = Color.FromArgb(34, 149, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Annuler", Width = 105, Height = 34, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(51, 73, 99), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        apply.Click += (_, _) => Save();
        buttons.Controls.Add(apply); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4); layout.SetColumnSpan(buttons, 3);
        Controls.Add(layout);
        AcceptButton = apply; CancelButton = cancel;
    }

    private void Save()
    {
        var result = new List<MultiSortCriterion>();
        for (var i = 0; i < 3; i++)
        {
            if (_fields[i].SelectedIndex <= 0)
            {
                if (i == 0)
                {
                    MessageBox.Show("Sélectionnez au moins le premier champ de tri.", "QNB - Tri", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                continue;
            }
            var field = _fields[i].SelectedItem?.ToString() ?? string.Empty;
            if (result.Any(x => string.Equals(x.Field, field, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Le champ « {field} » est déjà utilisé dans le tri.", "QNB - Tri", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            result.Add(new MultiSortCriterion(field, _directions[i].SelectedIndex == 0));
        }
        Criteria = result;
        DialogResult = DialogResult.OK;
        Close();
    }

    public static IReadOnlyList<MultiSortCriterion>? Select(IWin32Window owner, IReadOnlyList<string> fields, IReadOnlyList<MultiSortCriterion>? current = null)
    {
        using var dialog = new MultiColumnSortDialog(fields, current);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Criteria : null;
    }
}
