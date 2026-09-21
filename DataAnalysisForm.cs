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
        Text="QNB - Analyse des données";StartPosition=FormStartPosition.CenterParent;Size=new Size(760,300);MinimumSize=new Size(650,280);
        BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var title=new Label{Text="ANALYSE DES DONNÉES",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(22,0,0,0),Font=new Font("Segoe UI Semibold",20F,FontStyle.Bold)};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),BackColor=Color.FromArgb(4,36,73),FlowDirection=FlowDirection.LeftToRight,WrapContents=true};
        var ops=BankingRepository.LoadImports().SelectMany(x=>x.Operations).ToList();var min=ops.Count>0?ops.Min(x=>x.Date).Date:DateTime.Today;var max=ops.Count>0?ops.Max(x=>x.Date).Date:DateTime.Today;
        _from.Value=min;_to.Value=max;
        panel.Controls.Add(Label("Du"));panel.Controls.Add(_from);panel.Controls.Add(Label("au"));panel.Controls.Add(_to);
        var export=new Button{Text="Créer Excel",Width=150,Height=34,Margin=new Padding(20,0,0,0),BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};export.Click+=(_,_)=>Export();panel.Controls.Add(export);
        _status.Margin=new Padding(0,22,0,0);_status.Width=680;panel.SetFlowBreak(export,true);panel.Controls.Add(_status);
        Controls.Add(panel);Controls.Add(title);
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
        AddPie(synth,"Répartition des dépenses",2,sr-1,2,5);AddPie(synth,"Répartition des recettes",2,sr-1,3,22);
        wb.SaveAs(save.FileName);_status.Text=$"{rows.Count:N0} opérations exportées • {data.Count:N0} lignes mensuelles";MessageBox.Show("Le classeur Excel d'analyse a été créé.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    private static void AddPie(IXLWorksheet ws,string title,int first,int last,int valueColumn,int topRow)
    {
        if(last<first)return;var chart=ws.Charts.Add<IXLPieChart>();chart.Title.Text=title;chart.SetPosition(topRow,5);chart.SetSize(650,320);
        var series=chart.Series.Add(title);series.SetCategories(ws.Range(first,1,last,1));series.SetValues(ws.Range(first,valueColumn,last,valueColumn));
        series.DataLabels.ShowPercentage=true;series.DataLabels.ShowLeaderLines=true;chart.Legend.Position=XLChartLegendPosition.Right;
    }
}
