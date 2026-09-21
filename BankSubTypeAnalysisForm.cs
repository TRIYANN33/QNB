using ClosedXML.Excel;

namespace QNB;

internal sealed class BankSubTypeAnalysisForm : Form
{
    private readonly DateTimePicker _from=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly DateTimePicker _to=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly Label _status=new(){AutoSize=true,ForeColor=Color.FromArgb(183,207,229)};

    public BankSubTypeAnalysisForm() : this(null,null) { }

    public BankSubTypeAnalysisForm(DateTime? from,DateTime? to)
    {
        Text="QNB - Analyse par compte et S_Type";StartPosition=FormStartPosition.CenterParent;Size=new Size(800,310);MinimumSize=new Size(680,290);
        BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var title=new Label{Text="ANALYSE PAR COMPTE / S_TYPE",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(22,0,0,0),Font=new Font("Segoe UI Semibold",20F,FontStyle.Bold)};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),BackColor=Color.FromArgb(4,36,73),FlowDirection=FlowDirection.LeftToRight,WrapContents=true};
        var ops=BankingRepository.LoadImports().SelectMany(x=>x.Operations).ToList();var min=ops.Count>0?ops.Min(x=>x.Date).Date:DateTime.Today;var max=ops.Count>0?ops.Max(x=>x.Date).Date:DateTime.Today;_from.Value=from.HasValue&&from.Value>=_from.MinDate&&from.Value<=_from.MaxDate?from.Value:min;_to.Value=to.HasValue&&to.Value>=_to.MinDate&&to.Value<=_to.MaxDate?to.Value:max;
        panel.Controls.Add(L("Du"));panel.Controls.Add(_from);panel.Controls.Add(L("au"));panel.Controls.Add(_to);
        var export=new Button{Text="Créer nouveau classeur",Width=190,Height=34,Margin=new Padding(20,0,0,0),BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};export.Click+=(_,_)=>Export();panel.Controls.Add(export);
        panel.SetFlowBreak(export,true);_status.Margin=new Padding(0,22,0,0);_status.Width=720;panel.Controls.Add(_status);Controls.Add(panel);Controls.Add(title);
    }
    private static Label L(string s)=>new(){Text=s,AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(8,7,5,0)};
    private void Export()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations.Select(o=>new{Import=i,Op=o}))
            .Where(x=>x.Op.Date.Date>=_from.Value.Date&&x.Op.Date.Date<=_to.Value.Date)
            .Select(x=>new{Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"—":x.Import.BankName,Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,Op=x.Op,SubType=cls.TryGetValue(x.Op.Id,out var c)&&!string.IsNullOrWhiteSpace(c.SubType)?c.SubType:"Non typé"}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Compte_SType_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Compte par S_Type");
        ws.Cell(1,1).Value="Banque";ws.Cell(1,2).Value="Compte";ws.Cell(1,3).Value="S_Type";ws.Cell(1,4).Value="Dépenses";ws.Cell(1,5).Value="Recettes";ws.Cell(1,6).Value="Solde";
        var groups=rows.GroupBy(x=>new{x.Bank,x.Account,x.SubType}).OrderBy(x=>x.Key.Bank).ThenBy(x=>x.Key.Account).ThenBy(x=>x.Key.SubType).ToList();var r=2;
        foreach(var g in groups){var dep=g.Sum(x=>Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit)));var rec=g.Sum(x=>Math.Max(0m,x.Op.Debit+x.Op.Credit));ws.Cell(r,1).Value=g.Key.Bank;ws.Cell(r,2).Value=g.Key.Account;ws.Cell(r,3).Value=g.Key.SubType;ws.Cell(r,4).Value=dep;ws.Cell(r,5).Value=rec;ws.Cell(r,6).Value=rec-dep;r++;}
        ws.Range(1,1,r-1,6).CreateTable("AnalyseCompteSType");ws.Columns().AdjustToContents();ws.Columns(4,6).Style.NumberFormat.Format="#,##0.00 €";ws.SheetView.FreezeRows(1);
        var synth=wb.Worksheets.Add("Totaux comptes");synth.Cell(1,1).Value="Banque";synth.Cell(1,2).Value="Compte";synth.Cell(1,3).Value="Dépenses";synth.Cell(1,4).Value="Recettes";synth.Cell(1,5).Value="Solde";var sr=2;
        foreach(var g in rows.GroupBy(x=>new{x.Bank,x.Account}).OrderBy(x=>x.Key.Bank).ThenBy(x=>x.Key.Account)){var dep=g.Sum(x=>Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit)));var rec=g.Sum(x=>Math.Max(0m,x.Op.Debit+x.Op.Credit));synth.Cell(sr,1).Value=g.Key.Bank;synth.Cell(sr,2).Value=g.Key.Account;synth.Cell(sr,3).Value=dep;synth.Cell(sr,4).Value=rec;synth.Cell(sr,5).Value=rec-dep;sr++;}
        synth.Range(1,1,sr-1,5).CreateTable("TotauxComptes");synth.Columns().AdjustToContents();synth.Columns(3,5).Style.NumberFormat.Format="#,##0.00 €";
        var chartData=wb.Worksheets.Add("Graphique comptes");
        chartData.Cell(1,1).Value="Compte bancaire";chartData.Cell(1,2).Value="Dépenses";chartData.Cell(1,3).Value="Recettes";
        var cr=2;
        foreach(var g in rows.GroupBy(x=>new{x.Bank,x.Account}).OrderBy(x=>x.Key.Bank).ThenBy(x=>x.Key.Account))
        {
            chartData.Cell(cr,1).Value=$"{g.Key.Bank} — {g.Key.Account}";
            chartData.Cell(cr,2).Value=g.Sum(x=>Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit)));
            chartData.Cell(cr,3).Value=g.Sum(x=>Math.Max(0m,x.Op.Debit+x.Op.Credit));cr++;
        }
        chartData.Range(1,1,cr-1,3).CreateTable("DonneesGraphiqueComptes");chartData.Columns().AdjustToContents();chartData.Columns(2,3).Style.NumberFormat.Format="#,##0.00 €";
        chartData.Cell(cr+2,1).Value="GRAPHIQUE : Dépenses / Recettes par compte";
        chartData.Cell(cr+3,1).Value="Les données ci-dessus sont structurées pour créer un graphique Excel groupé.";
        wb.SaveAs(save.FileName);_status.Text=$"{rows.Count:N0} opérations • {groups.Count:N0} regroupements compte / S_Type";MessageBox.Show("Le nouveau classeur Excel a été créé.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
}