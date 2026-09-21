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
        var operations=BankingRepository.LoadOperationsForDuplicateAnalysis();var excluded=BankingRepository.LoadDuplicateExclusions();var results=new List<DuplicateCandidate>();
        foreach(var group in operations.GroupBy(x=>new{Amount=decimal.Round(x.Amount,2),Currency=Normalize(x.Currency)}))
        {
            var items=group.OrderBy(x=>x.Date).ToList();
            for(var i=0;i<items.Count;i++) for(var j=i+1;j<items.Count;j++)
            {
                var days=Math.Abs((items[j].Date.Date-items[i].Date.Date).Days);if(days>2)break;var key=$"{Math.Min(items[i].Id,items[j].Id)}:{Math.Max(items[i].Id,items[j].Id)}";if(excluded.Contains(key))continue;
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
    private readonly DataGridView _grid;
    private readonly Label _summary;
    private readonly Label _footerSummary;
    private readonly ComboBox _accountFilter;
    private readonly TextBox _selectedOperationInfo;
    private readonly TextBox _selectedOperationInfo2;
    private bool _groupCheckedRows;
    private List<DuplicateCandidate> _candidates = new();
    private IReadOnlyList<MultiSortCriterion> _sortCriteria = Array.Empty<MultiSortCriterion>();

    public DuplicateAnalysisForm()
    {
        Text="QNB - Analyse globale des doublons"; StartPosition=FormStartPosition.CenterParent; Size=new Size(1500,800); MinimumSize=new Size(1100,620); BackColor=Color.FromArgb(3,23,49); ForeColor=Color.White; Font=new Font("Segoe UI",9F);
        var header=new Panel{Dock=DockStyle.Top,Height=112,Padding=new Padding(18,12,18,8),BackColor=Color.FromArgb(4,36,73)};
        header.Controls.Add(new Label{Text="Analyse globale des doublons",AutoSize=true,Font=new Font("Segoe UI Semibold",16F,FontStyle.Bold),ForeColor=Color.White,Location=new Point(18,10)});
        _selectedOperationInfo=new TextBox{ReadOnly=true,Width=720,Height=28,Location=new Point(330,8),BackColor=Color.FromArgb(7,43,82),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI Semibold",9.5F),PlaceholderText="Opération 1 : Date • Libellé • Détail"};header.Controls.Add(_selectedOperationInfo);
        _selectedOperationInfo2=new TextBox{ReadOnly=true,Width=720,Height=28,Location=new Point(330,39),BackColor=Color.FromArgb(7,43,82),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI Semibold",9.5F),PlaceholderText="Opération 2 : Date • Libellé • Détail"};header.Controls.Add(_selectedOperationInfo2);
        header.Controls.Add(new Label{Text="Toutes les opérations impliquées dans un doublon sont affichées séparément • cochez celles que vous souhaitez supprimer",AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Location=new Point(20,76)});
        _summary=new Label{AutoSize=true,Anchor=AnchorStyles.Top|AnchorStyles.Right,ForeColor=Color.FromArgb(58,196,187),Font=new Font("Segoe UI Semibold",10F,FontStyle.Bold),Location=new Point(1150,44)}; header.Controls.Add(_summary);
        header.Controls.Add(new Label{Text="Compte :",AutoSize=true,ForeColor=Color.White,Location=new Point(620,78)});
        _accountFilter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=300,Location=new Point(680,74)};
        _accountFilter.SelectedIndexChanged+=(_,_)=>RenderCandidates(); header.Controls.Add(_accountFilter);

        _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=false,BackgroundColor=Color.FromArgb(7,42,78),SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,RowHeadersVisible=false,EnableHeadersVisualStyles=false};
        _grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(8,73,137); _grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        _grid.DefaultCellStyle.BackColor=Color.FromArgb(7,43,82); _grid.DefaultCellStyle.ForeColor=Color.White; _grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(18,82,146); _grid.DefaultCellStyle.SelectionForeColor=Color.White;
        AddColumns();
        foreach(DataGridViewColumn column in _grid.Columns) if(column is not DataGridViewCheckBoxColumn) column.ReadOnly=true;
        _grid.SelectionChanged+=(_,_)=>UpdateSelectedOperationInfo();
        _grid.CellClick+=(_,_)=>UpdateSelectedOperationInfo();
        _grid.CellValueChanged+=(_,e)=>{if(e.RowIndex>=0&&_grid.Columns[e.ColumnIndex].Name=="Choix")GroupCheckedRows();};
        _grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(_grid.IsCurrentCellDirty&&_grid.CurrentCell is DataGridViewCheckBoxCell)_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};

        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10),BackColor=Color.FromArgb(4,36,73)};
        var close=new Button{Text="Fermer",Width=110,Height=34,DialogResult=DialogResult.Cancel};
        var refresh=new Button{Text="Relancer l'analyse",Width=150,Height=34,BackColor=Color.FromArgb(34,149,255),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var sort=new Button{Text="Tri 3 champs",Width=135,Height=34,BackColor=Color.FromArgb(16,112,187),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var notDuplicate=new Button{Text="Pas un doublon",Width=145,Height=34,BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var showExcluded=new Button{Text="Réafficher les exclus",Width=165,Height=34,BackColor=Color.FromArgb(100,90,150),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var delete=new Button{Text="✕ Supprimer cochées",Width=185,Height=34,BackColor=Color.FromArgb(190,48,58),ForeColor=Color.White,Font=new Font("Segoe UI Semibold",9.3F,FontStyle.Bold),FlatStyle=FlatStyle.Flat};
        refresh.Click+=(_,_)=>LoadCandidates(); sort.Click+=(_,_)=>ConfigureSort(); delete.Click+=(_,_)=>DeleteChecked(); notDuplicate.Click+=(_,_)=>MarkNotDuplicate(); showExcluded.Click+=(_,_)=>RestoreExcluded();
        footer.Controls.Add(close); footer.Controls.Add(refresh); footer.Controls.Add(sort); footer.Controls.Add(showExcluded); footer.Controls.Add(notDuplicate); footer.Controls.Add(delete);
        _footerSummary=new Label{AutoSize=true,ForeColor=Color.White,Font=new Font("Segoe UI Semibold",10F,FontStyle.Bold),Margin=new Padding(18,8,24,0),TextAlign=ContentAlignment.MiddleLeft};
        footer.Controls.Add(_footerSummary);
        Controls.Add(_grid); Controls.Add(footer); Controls.Add(header); Shown+=(_,_)=>LoadCandidates();
    }

    private void AddColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Choix",HeaderText="Choix",Width=55,ReadOnly=false,SortMode=DataGridViewColumnSortMode.NotSortable});
        Add("Niveau",75); Add("Score",55); Add("Date",85); Add("Montant",95); Add("Banque",120); Add("Compte",135); Add("Libellé",300); Add("Détails",300); Add("Motif",330);
        _grid.Columns.Add(new DataGridViewTextBoxColumn{Name="OperationId",Visible=false});
    }
    private void Add(string name,int width)=>_grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=name,Width=width,SortMode=DataGridViewColumnSortMode.NotSortable});

    private void ConfigureSort()
    {
        var fields=new[]{"Niveau","Score","Date","Montant","Banque","Compte","Libellé","Détails","Motif"};
        var selected=MultiColumnSortDialog.Select(this,fields,_sortCriteria); if(selected is null)return; _sortCriteria=selected; RenderCandidates();
    }

    private void LoadCandidates()
    {
        Cursor=Cursors.WaitCursor; try{_candidates=DuplicateAnalysisService.Analyze();ReloadAccountFilter();RenderCandidates();}finally{Cursor=Cursors.Default;}
    }

    private void ReloadAccountFilter()
    {
        var current=_accountFilter.SelectedItem?.ToString()??"Tous les comptes";
        // La liste ne propose que les comptes qui ont encore au moins une paire de doublons interne au même compte.
        var accounts=_candidates.Where(x=>string.Equals(AccountKey(x.Left),AccountKey(x.Right),StringComparison.CurrentCultureIgnoreCase)).Select(x=>AccountKey(x.Left)).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x=>x).ToList();
        _accountFilter.BeginUpdate();_accountFilter.Items.Clear();_accountFilter.Items.Add("Tous les comptes");foreach(var account in accounts)_accountFilter.Items.Add(account);
        var index=_accountFilter.Items.IndexOf(current);_accountFilter.SelectedIndex=index>=0?index:0;_accountFilter.EndUpdate();
    }

    private void RenderCandidates()
    {
        // Une opération peut apparaître dans plusieurs paires. On ne l'affiche qu'une fois,
        // tout en conservant le meilleur score/motif rencontré.
        var selectedAccount=_accountFilter.SelectedItem?.ToString();
        var candidates=string.IsNullOrWhiteSpace(selectedAccount)||selectedAccount=="Tous les comptes"?_candidates:_candidates.Where(x=>string.Equals(AccountKey(x.Left),selectedAccount,StringComparison.CurrentCultureIgnoreCase)&&string.Equals(AccountKey(x.Right),selectedAccount,StringComparison.CurrentCultureIgnoreCase)).ToList();
        var rows=candidates
            .SelectMany(c=>new[]{new DuplicateLine(c,c.Left),new DuplicateLine(c,c.Right)})
            .GroupBy(x=>x.Operation.Id)
            .Select(g=>g.OrderByDescending(x=>x.Candidate.Score).First())
            .ToList();

        var selectors=new Dictionary<string,Func<DuplicateLine,object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Niveau"]=x=>x.Candidate.Level,["Score"]=x=>x.Candidate.Score,["Date"]=x=>x.Operation.Date,["Montant"]=x=>x.Operation.Amount,
            ["Banque"]=x=>x.Operation.Bank,["Compte"]=x=>x.Operation.Account,["Libellé"]=x=>BestLabel(x.Operation),["Détails"]=x=>x.Operation.Details,["Motif"]=x=>x.Candidate.Reason
        };
        var sorted=MultiColumnSorter.Apply(rows,_sortCriteria,selectors);
        _grid.Rows.Clear(); var culture=CultureInfo.GetCultureInfo("fr-FR");
        foreach(var line in sorted)
            _grid.Rows.Add(false,line.Candidate.Level,line.Candidate.Score+"%",line.Operation.Date.ToString("dd/MM/yyyy"),line.Operation.Amount.ToString("N2",culture),line.Operation.Bank,line.Operation.Account,BestLabel(line.Operation),line.Operation.Details,line.Candidate.Reason,line.Operation.Id);
        var totalRecords=BankingRepository.GetDashboardStats().Operations;
        _summary.Text=$"{rows.Count} opération(s) en doublon • {candidates.Count} paire(s)";
        _footerSummary.Text=$"Doublons : {rows.Count:N0} / {totalRecords:N0} enregistrement(s)";
    }

    private void MarkNotDuplicate()
    {
        _grid.EndEdit();var selected=new List<long>();foreach(DataGridViewRow row in _grid.Rows)if(Convert.ToBoolean(row.Cells["Choix"].Value??false)&&long.TryParse(Convert.ToString(row.Cells["OperationId"].Value),out var id))selected.Add(id);
        selected=selected.Distinct().ToList();if(selected.Count<2){MessageBox.Show("Cochez au moins 2 opérations qui ne sont pas des doublons.","QNB - Doublons",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var pairs=0;for(var i=0;i<selected.Count;i++)for(var j=i+1;j<selected.Count;j++){BankingRepository.ExcludeDuplicatePair(selected[i],selected[j]);pairs++;}
        LoadCandidates();MessageBox.Show($"{selected.Count} opération(s) traitée(s) • {pairs} paire(s) marquée(s) « Pas un doublon ».","QNB - Doublons",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    private void RestoreExcluded()
    {
        var answer=MessageBox.Show("Réafficher toutes les paires précédemment marquées « Pas un doublon » ?","QNB - Doublons",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
        if(answer!=DialogResult.Yes)return;BankingRepository.ClearDuplicateExclusions();LoadCandidates();
    }

    private void GroupCheckedRows()
    {
        if(_groupCheckedRows)return;
        UpdateSelectedOperationInfo();
        var checkedIds=_grid.Rows.Cast<DataGridViewRow>().Where(r=>Convert.ToBoolean(r.Cells["Choix"].Value??false)).Select(r=>Convert.ToInt64(r.Cells["OperationId"].Value)).ToList();
        if(checkedIds.Count!=2)return;
        var first=_grid.Rows.Cast<DataGridViewRow>().First(r=>Convert.ToInt64(r.Cells["OperationId"].Value)==checkedIds[0]);
        var second=_grid.Rows.Cast<DataGridViewRow>().First(r=>Convert.ToInt64(r.Cells["OperationId"].Value)==checkedIds[1]);
        if(first.Index+1==second.Index)return;
        _groupCheckedRows=true;
        try
        {
            var values=second.Cells.Cast<DataGridViewCell>().Select(x=>x.Value).ToArray();
            _grid.Rows.Remove(second);
            var index=Math.Min(first.Index+1,_grid.Rows.Count);
            _grid.Rows.Insert(index,values);
            _grid.Rows[index].Cells["Choix"].Value=true;
        }
        finally{_groupCheckedRows=false;}
    }

    private void UpdateSelectedOperationInfo()
    {
        var rows=_grid.Rows.Cast<DataGridViewRow>().Where(r=>Convert.ToBoolean(r.Cells["Choix"].Value??false)).Take(2).ToList();
        if(rows.Count==0 && _grid.CurrentRow is not null)rows.Add(_grid.CurrentRow);
        string Info(DataGridViewRow row){var date=Convert.ToString(row.Cells["Date"].Value)??string.Empty;var label=Convert.ToString(row.Cells["Libellé"].Value)??string.Empty;var details=Convert.ToString(row.Cells["Détails"].Value)??string.Empty;return $"{date}   •   {label}   •   {(string.IsNullOrWhiteSpace(details)?"—":details)}";}
        _selectedOperationInfo.Text=rows.Count>0?Info(rows[0]):string.Empty;
        _selectedOperationInfo2.Text=rows.Count>1?Info(rows[1]):string.Empty;
    }

    private void DeleteChecked()
    {
        _grid.EndEdit();
        var ids=new List<long>();
        foreach(DataGridViewRow row in _grid.Rows)
            if(Convert.ToBoolean(row.Cells["Choix"].Value??false) && long.TryParse(Convert.ToString(row.Cells["OperationId"].Value),out var id)) ids.Add(id);
        ids=ids.Distinct().ToList();
        if(ids.Count==0){MessageBox.Show("Cochez au moins une ligne à supprimer.","QNB - Doublons",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var answer=MessageBox.Show($"Supprimer définitivement {ids.Count} opération(s) cochée(s) ?\n\nLes lignes non cochées seront conservées. Cette action est irréversible.","QNB - Confirmer",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
        if(answer!=DialogResult.Yes)return;
        BankingRepository.DeleteOperations(ids); LoadCandidates();
        MessageBox.Show($"{ids.Count} opération(s) supprimée(s).","QNB - Doublons",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private static string AccountKey(DuplicateOperation operation)=>$"{operation.Bank} — {operation.Account}";
    private static string BestLabel(DuplicateOperation operation)=>string.IsNullOrWhiteSpace(operation.Label)?operation.Nature:operation.Label;
    private sealed record DuplicateLine(DuplicateCandidate Candidate,DuplicateOperation Operation);
}
