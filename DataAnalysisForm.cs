using System.Globalization;
using ClosedXML.Excel;

namespace QNB;

internal sealed class DataAnalysisForm : Form
{
    private readonly DateTimePicker _from=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly DateTimePicker _to=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly Label _status=new(){AutoSize=true,ForeColor=Color.FromArgb(183,207,229)};

    public DataAnalysisForm()
    {
        Text="QNB - Analyse des données";StartPosition=FormStartPosition.CenterParent;Size=new Size(920,380);MinimumSize=new Size(760,340);
        BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var title=new Label{Text="ANALYSE DES DONNÉES",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(22,0,0,0),Font=new Font("Segoe UI Semibold",20F,FontStyle.Bold)};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),BackColor=Color.FromArgb(4,36,73),FlowDirection=FlowDirection.LeftToRight,WrapContents=true};
        var ops=BankingRepository.LoadImports().SelectMany(x=>x.Operations).ToList();var min=ops.Count>0?ops.Min(x=>x.Date).Date:DateTime.Today;var max=ops.Count>0?ops.Max(x=>x.Date).Date:DateTime.Today;
        _from.Value=min;_to.Value=max;
        panel.Controls.Add(Label("Du"));panel.Controls.Add(_from);panel.Controls.Add(Label("au"));panel.Controls.Add(_to);
        var export=new Button{Text="Analyse mensuelle",Width=160,Height=34,Margin=new Padding(20,0,0,0),BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};export.Click+=(_,_)=>Export();panel.Controls.Add(export);
        var account=new Button{Text="Compte / S_Type",Width=160,Height=34,Margin=new Padding(8,0,0,0),BackColor=Color.FromArgb(16,112,187),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};account.Click+=(_,_)=>{using var x=new BankSubTypeAnalysisForm(_from.Value.Date,_to.Value.Date);x.ShowDialog(this);};panel.Controls.Add(account);
        var ledger=new Button{Text="Grand livre comptable",Width=190,Height=34,Margin=new Padding(8,0,0,0),BackColor=Color.FromArgb(120,82,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};ledger.Click+=(_,_)=>ExportLedger();panel.Controls.Add(ledger);
        _status.Margin=new Padding(0,22,0,0);_status.Width=680;panel.SetFlowBreak(export,true);panel.Controls.Add(_status);
        Controls.Add(panel);Controls.Add(title);
    }
    private void ExportLedger()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations.Select(o=>new{Import=i,Op=o})).Where(x=>x.Op.Date.Date>=_from.Value.Date&&x.Op.Date.Date<=_to.Value.Date)
            .Select(x=>{cls.TryGetValue(x.Op.Id,out var k);return new{Bank=x.Import.BankName,Account=x.Import.AccountDisplayName,Op=x.Op,Type=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type,SubType=string.IsNullOrWhiteSpace(k?.SubType)?"Non typé":k.SubType};}).OrderBy(x=>x.Op.Date).ThenBy(x=>x.Op.Id).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Grand_Livre_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Grand livre");
        var headers=new[]{"Date","Banque","Compte","Type","S_Type","Libellé","Détails","Débit","Crédit","Solde cumulé"};for(var i=0;i<headers.Length;i++)ws.Cell(1,i+1).Value=headers[i];
        decimal balance=0;var r=2;
        foreach(var x in rows){var debit=Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit));var credit=Math.Max(0m,x.Op.Debit+x.Op.Credit);balance+=credit-debit;ws.Cell(r,1).Value=x.Op.Date;ws.Cell(r,2).Value=x.Bank;ws.Cell(r,3).Value=x.Account;ws.Cell(r,4).Value=x.Type;ws.Cell(r,5).Value=x.SubType;ws.Cell(r,6).Value=x.Op.Label;ws.Cell(r,7).Value=x.Op.Details;ws.Cell(r,8).Value=debit;ws.Cell(r,9).Value=credit;ws.Cell(r,10).Value=balance;r++;}
        ws.Range(1,1,r-1,10).CreateTable("GrandLivre");ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(8,10).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);wb.SaveAs(save.FileName);
        _status.Text=$"Grand livre créé : {rows.Count:N0} écritures";MessageBox.Show("Le grand livre comptable a été créé.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private static Label Label(string s)=>new(){Text=s,AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(8,7,5,0)};
    private void Export()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations).Where(o=>o.Date.Date>=_from.Value.Date&&o.Date.Date<=_to.Value.Date)
            .Select(o=>new{Op=o,Type=cls.TryGetValue(o.Id,out var c)&&!string.IsNullOrWhiteSpace(c.Type)?c.Type:"Non typé"}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Analyse_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Analyse");
        ws.Cell(1,1).Value="Mois";ws.Cell(1,2).Value="Type";ws.Cell(1,3).Value="Dépenses";ws.Cell(1,4).Value="Recettes";
        var data=rows.GroupBy(x=>new{x.Op.Date.Year,x.Op.Date.Month,x.Type}).OrderBy(x=>x.Key.Year).ThenBy(x=>x.Key.Month).ThenBy(x=>x.Key.Type).ToList();var r=2;
        foreach(var g in data){ws.Cell(r,1).Value=new DateTime(g.Key.Year,g.Key.Month,1);ws.Cell(r,1).Style.DateFormat.Format="mmmm yyyy";ws.Cell(r,2).Value=g.Key.Type;ws.Cell(r,3).Value=g.Sum(x=>Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit)));ws.Cell(r,4).Value=g.Sum(x=>Math.Max(0m,x.Op.Debit+x.Op.Credit));r++;}
        ws.Range(1,1,r-1,4).CreateTable("AnalyseMensuelle");ws.Columns().AdjustToContents();ws.Columns(3,4).Style.NumberFormat.Format="#,##0.00 €";
        var synth=wb.Worksheets.Add("Répartition");
        synth.Cell(1,1).Value="Type";synth.Cell(1,2).Value="Dépenses";synth.Cell(1,3).Value="Recettes";var totals=rows.GroupBy(x=>x.Type).OrderBy(x=>x.Key).ToList();var sr=2;
        foreach(var g in totals){synth.Cell(sr,1).Value=g.Key;synth.Cell(sr,2).Value=g.Sum(x=>Math.Abs(Math.Min(0m,x.Op.Debit+x.Op.Credit)));synth.Cell(sr,3).Value=g.Sum(x=>Math.Max(0m,x.Op.Debit+x.Op.Credit));sr++;}
        synth.Columns().AdjustToContents();synth.Columns(2,3).Style.NumberFormat.Format="#,##0.00 €";
        // Les données de répartition sont prêtes pour les graphiques Excel.
        // ClosedXML 0.104 ne fournit pas d'API de création de graphiques.
        wb.SaveAs(save.FileName);_status.Text=$"{rows.Count:N0} opérations exportées • {data.Count:N0} lignes mensuelles";MessageBox.Show("Le classeur Excel d'analyse a été créé.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

}
