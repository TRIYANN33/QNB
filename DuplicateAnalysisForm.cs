using System.Globalization;
using System.Text;

namespace QNB;

internal sealed class DuplicateButtonMessageFilter : IMessageFilter
{
    private const int WmLButtonDown = 0x0201;
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmLButtonDown) return false;
        if (Control.FromHandle(m.HWnd) is not Button button || !button.Text.Contains("Analyser les doublons", StringComparison.OrdinalIgnoreCase)) return false;
        using var form = new DuplicateAnalysisForm();
        form.ShowDialog(button.FindForm());
        return true;
    }
}

internal sealed class DuplicateCandidate { public string Level { get; init; }=string.Empty; public int Score { get; init; } public string Reason { get; init; }=string.Empty; public DuplicateOperation Left { get; init; }=null!; public DuplicateOperation Right { get; init; }=null!; }
internal sealed class DuplicateOperation { public long Id { get; init; } public string Bank { get; init; }=string.Empty; public string Account { get; init; }=string.Empty; public DateTime Date { get; init; } public DateTime? ValueDate { get; init; } public decimal Amount { get; init; } public string Currency { get; init; }=string.Empty; public string Nature { get; init; }=string.Empty; public string Label { get; init; }=string.Empty; public string Details { get; init; }=string.Empty; }

internal static class DuplicateAnalysisService
{
    public static List<DuplicateCandidate> Analyze()
    {
        var operations=BankingRepository.LoadOperationsForDuplicateAnalysis();var results=new List<DuplicateCandidate>();
        foreach(var group in operations.GroupBy(x=>new{Amount=decimal.Round(x.Amount,2),Currency=Normalize(x.Currency)}))
        {
            var items=group.OrderBy(x=>x.Date).ToList();
            for(var i=0;i<items.Count;i++) for(var j=i+1;j<items.Count;j++)
            {
                var days=Math.Abs((items[j].Date.Date-items[i].Date.Date).Days);if(days>2)break;
                var similarity=Similarity(Normalize($"{items[i].Nature} {items[i].Label} {items[i].Details}"),Normalize($"{items[j].Nature} {items[j].Label} {items[j].Details}"));
                var sameValueDate=items[i].ValueDate.HasValue&&items[j].ValueDate.HasValue&&items[i].ValueDate.Value.Date==items[j].ValueDate.Value.Date;
                var score=45+(days==0?30:days==1?18:10)+(int)Math.Round(similarity*25m)+(sameValueDate?5:0);score=Math.Min(score,100);if(score<70)continue;
                results.Add(new DuplicateCandidate{Level=score>=90?"Certain":"Probable",Score=score,Reason=$"Même montant {items[i].Amount:N2} {items[i].Currency}; dates à {days} jour(s); libellés similaires à {similarity:P0}",Left=items[i],Right=items[j]});
            }
        }
        return results.OrderByDescending(x=>x.Score).ThenByDescending(x=>x.Left.Date).ToList();
    }
    private static string Normalize(string? value){if(string.IsNullOrWhiteSpace(value))return string.Empty;var d=value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);var sb=new StringBuilder();var space=false;foreach(var ch in d){if(CharUnicodeInfo.GetUnicodeCategory(ch)==UnicodeCategory.NonSpacingMark)continue;if(char.IsLetterOrDigit(ch)){sb.Append(ch);space=false;}else if(!space){sb.Append(' ');space=true;}}return sb.ToString().Trim();}
    private static decimal Similarity(string left,string right){if(left==right&&left.Length>0)return 1m;var a=left.Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);var b=right.Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);if(a.Count==0||b.Count==0)return 0m;var intersection=a.Count(x=>b.Contains(x));var union=a.Union(b).Count();return union==0?0m:(decimal)intersection/union;}
}

internal sealed class DuplicateAnalysisForm : Form
{
    private readonly DataGridView _grid;private readonly Label _summary;
    public DuplicateAnalysisForm()
    {
        Text="QNB - Analyse globale des doublons";StartPosition=FormStartPosition.CenterParent;Size=new Size(1450,780);MinimumSize=new Size(1050,600);BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",9F);
        var header=new Panel{Dock=DockStyle.Top,Height=82,Padding=new Padding(18,12,18,8),BackColor=Color.FromArgb(4,36,73)};header.Controls.Add(new Label{Text="Analyse globale des doublons",AutoSize=true,Font=new Font("Segoe UI Semibold",16F,FontStyle.Bold),ForeColor=Color.White,Location=new Point(18,10)});header.Controls.Add(new Label{Text="Comparaison indépendante de la banque et du compte • montant, date, devise et similarité des libellés",AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Location=new Point(20,44)});_summary=new Label{AutoSize=true,Anchor=AnchorStyles.Top|AnchorStyles.Right,ForeColor=Color.FromArgb(58,196,187),Font=new Font("Segoe UI Semibold",10F,FontStyle.Bold),Location=new Point(1080,28)};header.Controls.Add(_summary);
        _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.FromArgb(7,42,78),ForeColor=Color.FromArgb(20,30,45),SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};AddColumns();
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10),BackColor=Color.FromArgb(4,36,73)};var close=new Button{Text="Fermer",Width=110,Height=34,DialogResult=DialogResult.Cancel};var refresh=new Button{Text="Relancer l'analyse",Width=150,Height=34,BackColor=Color.FromArgb(34,149,255),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};refresh.Click+=(_,_)=>LoadCandidates();footer.Controls.Add(close);footer.Controls.Add(refresh);Controls.Add(_grid);Controls.Add(footer);Controls.Add(header);Shown+=(_,_)=>LoadCandidates();
    }
    private void AddColumns(){Add("Niveau",75);Add("Score",55);Add("Date A",85);Add("Montant A",95);Add("Banque A",120);Add("Compte A",130);Add("Libellé A",220);Add("Date B",85);Add("Montant B",95);Add("Banque B",120);Add("Compte B",130);Add("Libellé B",220);Add("Motif",330);}
    private void Add(string name,int width)=>_grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=name,Width=width});
    private void LoadCandidates(){Cursor=Cursors.WaitCursor;try{var candidates=DuplicateAnalysisService.Analyze();_grid.Rows.Clear();var culture=CultureInfo.GetCultureInfo("fr-FR");foreach(var c in candidates)_grid.Rows.Add(c.Level,c.Score+"%",c.Left.Date.ToString("dd/MM/yyyy"),c.Left.Amount.ToString("N2",culture),c.Left.Bank,c.Left.Account,BestLabel(c.Left),c.Right.Date.ToString("dd/MM/yyyy"),c.Right.Amount.ToString("N2",culture),c.Right.Bank,c.Right.Account,BestLabel(c.Right),c.Reason);_summary.Text=$"{candidates.Count} paire(s) détectée(s)";}finally{Cursor=Cursors.Default;}}
    private static string BestLabel(DuplicateOperation operation)=>string.IsNullOrWhiteSpace(operation.Label)?operation.Nature:operation.Label;
}
