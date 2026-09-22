using System.Diagnostics;
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
        var journal=new Button{Text="Journal mensuel",Width=160,Height=34,Margin=new Padding(8,0,0,0),BackColor=Color.FromArgb(174,112,38),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};journal.Click+=(_,_)=>ExportJournal();panel.Controls.Add(journal);
        var typeJournal=new Button{Text="Journal par Type",Width=160,Height=34,Margin=new Padding(8,0,0,0),BackColor=Color.FromArgb(16,112,187),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};typeJournal.Click+=(_,_)=>ExportTypeJournal();panel.Controls.Add(typeJournal);
        var postJournal=new Button{Text="Journal annuel / Poste",Width=185,Height=34,Margin=new Padding(8,0,0,0),BackColor=Color.FromArgb(92,82,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};postJournal.Click+=(_,_)=>ExportAnnualPostJournal();panel.Controls.Add(postJournal);
        _status.Margin=new Padding(0,22,0,0);_status.Width=680;panel.SetFlowBreak(journal,true);panel.Controls.Add(_status);
        Controls.Add(panel);Controls.Add(title);
    }
    private void ExportAnnualPostJournal()
    {
        var year=_from.Value.Year;var from=new DateTime(year,1,1);var to=new DateTime(year,12,31);
        var cls=BankingRepository.LoadOperationClassifications();var ruleColors=BankingRepository.LoadClassificationRules().Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.CellColor)).OrderBy(x=>x.Priority).ToList();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations.Select(o=>new{Import=i,Op=o})).Where(x=>x.Op.Date.Date>=from&&x.Op.Date.Date<=to)
            .Select(x=>{cls.TryGetValue(x.Op.Id,out var k);return new{Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"—":x.Import.BankName,Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,Op=x.Op,Post=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type,SubType=string.IsNullOrWhiteSpace(k?.SubType)?"Non typé":k.SubType};}).ToList();
        if(rows.Count==0){MessageBox.Show($"Aucune opération pour l'année {year}.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Journal_Par_Poste_{year}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add($"Postes {year}");var r=1;
        ws.Cell(r,1).Value="ANNÉE";ws.Cell(r,2).Value=year;ws.Range(r,1,r,8).Style.Font.Bold=true;r+=2;
        foreach(var post in rows.GroupBy(x=>x.Post).OrderBy(x=>x.Key))
        {
            ws.Cell(r,1).Value="POSTE";ws.Cell(r,2).Value=post.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;
            var rule=ruleColors.FirstOrDefault(x=>string.Equals(x.Type,post.Key,StringComparison.CurrentCultureIgnoreCase));if(rule is not null)try{ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.FromHtml(rule.CellColor);var color=ColorTranslator.FromHtml(rule.CellColor);ws.Range(r,1,r,8).Style.Font.FontColor=(color.R*299+color.G*587+color.B*114)/1000>140?XLColor.Black:XLColor.White;}catch{}r++;
            foreach(var sub in post.GroupBy(x=>x.SubType).OrderBy(x=>x.Key))
            {
                ws.Cell(r,1).Value="S_TYPE";ws.Cell(r,2).Value=sub.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                var headers=new[]{"Date","A-Z","Détails","Compte","S_Type","Débit","Crédit","Solde"};for(var i=0;i<headers.Length;i++)ws.Cell(r,i+1).Value=headers[i];ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                decimal debitTotal=0,creditTotal=0;var alternate=false;
                foreach(var x in sub.OrderBy(x=>x.Op.Date).ThenBy(x=>x.Op.Id)){var debit=Math.Abs(Math.Min(0m,x.Op.Amount));var credit=Math.Max(0m,x.Op.Amount);debitTotal+=debit;creditTotal+=credit;ws.Cell(r,1).Value=x.Op.Date;ws.Cell(r,2).Value=string.IsNullOrWhiteSpace(x.Op.InterbankLabel)?x.Op.Nature:x.Op.InterbankLabel;ws.Cell(r,3).Value=x.Op.Details;ws.Cell(r,4).Value=$"{x.Bank} - {x.Account}";ws.Cell(r,5).Value=x.SubType;ws.Cell(r,6).Value=debit;ws.Cell(r,7).Value=credit;ws.Cell(r,8).Value=credit-debit;if(alternate)ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.LightBlue;if(debit>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;if(credit>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;ws.Cell(r,8).Style.Font.FontColor=(credit-debit)<0?XLColor.Red:XLColor.Green;alternate=!alternate;r++;}
                ws.Cell(r,5).Value="Total";ws.Cell(r,6).Value=debitTotal;ws.Cell(r,7).Value=creditTotal;ws.Cell(r,8).Value=creditTotal-debitTotal;ws.Range(r,5,r,8).Style.Font.Bold=true;if(debitTotal>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;if(creditTotal>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;ws.Cell(r,8).Style.Font.FontColor=(creditTotal-debitTotal)<0?XLColor.Red:XLColor.Green;r+=2;
            }r++;
        }
        ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(6,8).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);wb.SaveAs(save.FileName);_status.Text=$"Journal annuel par Poste {year} : {rows.Count:N0} opérations";
        try{Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true});}catch(Exception ex){MessageBox.Show($"Le journal a bien été créé, mais son ouverture automatique a échoué.\n\n{ex.Message}","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}MessageBox.Show($"Le journal annuel par Poste {year} a été créé et ouvert dans Excel.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private void ExportTypeJournal()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var ruleColors=BankingRepository.LoadClassificationRules().Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.CellColor)).OrderBy(x=>x.Priority).ToList();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations.Select(o=>new{Import=i,Op=o})).Where(x=>x.Op.Date.Date>=_from.Value.Date&&x.Op.Date.Date<=_to.Value.Date)
            .Select(x=>{cls.TryGetValue(x.Op.Id,out var k);return new{Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"—":x.Import.BankName,Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,Op=x.Op,Type=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type,SubType=string.IsNullOrWhiteSpace(k?.SubType)?"Non typé":k.SubType};}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Journal_Par_Type_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Journal par Type");var r=1;
        foreach(var type in rows.GroupBy(x=>x.Type).OrderBy(x=>x.Key))
        {
            ws.Cell(r,1).Value="TYPE";ws.Cell(r,2).Value=type.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;
            var typeRule=ruleColors.FirstOrDefault(x=>string.Equals(x.Type,type.Key,StringComparison.CurrentCultureIgnoreCase));
            if(typeRule is not null)try{ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.FromHtml(typeRule.CellColor);var color=ColorTranslator.FromHtml(typeRule.CellColor);ws.Range(r,1,r,8).Style.Font.FontColor=(color.R*299+color.G*587+color.B*114)/1000>140?XLColor.Black:XLColor.White;}catch{} r++;
            foreach(var sub in type.GroupBy(x=>x.SubType).OrderBy(x=>x.Key))
            {
                ws.Cell(r,1).Value="S_TYPE";ws.Cell(r,2).Value=sub.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                var headers=new[]{"Date","A-Z","Détails","Compte","S_Type","Débit","Crédit","Solde"};for(var i=0;i<headers.Length;i++)ws.Cell(r,i+1).Value=headers[i];ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                decimal subtotalDebit=0,subtotalCredit=0;var alternate=false;
                foreach(var x in sub.OrderBy(x=>x.Op.Date).ThenBy(x=>x.Op.Id))
                {
                    var debit=Math.Abs(Math.Min(0m,x.Op.Amount));var credit=Math.Max(0m,x.Op.Amount);subtotalDebit+=debit;subtotalCredit+=credit;
                    ws.Cell(r,1).Value=x.Op.Date;ws.Cell(r,2).Value=string.IsNullOrWhiteSpace(x.Op.InterbankLabel)?x.Op.Nature:x.Op.InterbankLabel;ws.Cell(r,3).Value=x.Op.Details;ws.Cell(r,4).Value=$"{x.Bank} - {x.Account}";ws.Cell(r,5).Value=x.SubType;ws.Cell(r,6).Value=debit;ws.Cell(r,7).Value=credit;ws.Cell(r,8).Value=credit-debit;
                    if(alternate)ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.LightBlue;if(debit>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;if(credit>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;ws.Cell(r,8).Style.Font.FontColor=(credit-debit)<0?XLColor.Red:XLColor.Green;alternate=!alternate;r++;
                }
                ws.Cell(r,5).Value="Total";ws.Cell(r,6).Value=subtotalDebit;ws.Cell(r,7).Value=subtotalCredit;ws.Cell(r,8).Value=subtotalCredit-subtotalDebit;ws.Range(r,5,r,8).Style.Font.Bold=true;if(subtotalDebit>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;if(subtotalCredit>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;ws.Cell(r,8).Style.Font.FontColor=(subtotalCredit-subtotalDebit)<0?XLColor.Red:XLColor.Green;r+=2;
            }
            r++;
        }
        ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(6,8).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);wb.SaveAs(save.FileName);_status.Text=$"Journal par Type créé : {rows.Count:N0} opérations";
        try{Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true});}catch(Exception ex){MessageBox.Show($"Le journal a bien été créé, mais son ouverture automatique a échoué.\n\n{ex.Message}","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        MessageBox.Show("Le journal regroupé par Type a été créé et ouvert dans Excel.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private void ExportJournal()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var ruleColors=BankingRepository.LoadClassificationRules().Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.CellColor)).OrderBy(x=>x.Priority).ToList();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations.Select(o=>new{Import=i,Op=o}))
            .Where(x=>x.Op.Date.Date>=_from.Value.Date&&x.Op.Date.Date<=_to.Value.Date)
            .Select(x=>{cls.TryGetValue(x.Op.Id,out var k);return new{Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"—":x.Import.BankName,Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,Op=x.Op,Type=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type,SubType=string.IsNullOrWhiteSpace(k?.SubType)?"Non typé":k.SubType};}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Journal_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Journal");var r=1;
        foreach(var account in rows.GroupBy(x=>new{x.Bank,x.Account}).OrderBy(x=>x.Key.Bank).ThenBy(x=>x.Key.Account))
        {
            ws.Cell(r,1).Value="COMPTE";ws.Cell(r,2).Value=$"{account.Key.Bank} - {account.Key.Account}";ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
            foreach(var month in account.GroupBy(x=>new{x.Op.Date.Year,x.Op.Date.Month}).OrderBy(x=>x.Key.Year).ThenBy(x=>x.Key.Month))
            {
                ws.Cell(r,1).Value="MOIS";ws.Cell(r,2).Value=new DateTime(month.Key.Year,month.Key.Month,1);ws.Cell(r,2).Style.DateFormat.Format="mmmm yyyy";ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                foreach(var type in month.GroupBy(x=>x.Type).OrderBy(x=>x.Key))
                {
                    ws.Cell(r,1).Value="TYPE";ws.Cell(r,2).Value=type.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;
                    var typeRule=ruleColors.FirstOrDefault(x=>string.Equals(x.Type,type.Key,StringComparison.CurrentCultureIgnoreCase));
                    if(typeRule is not null)try{ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.FromHtml(typeRule.CellColor);var color=ColorTranslator.FromHtml(typeRule.CellColor);ws.Range(r,1,r,8).Style.Font.FontColor=(color.R*299+color.G*587+color.B*114)/1000>140?XLColor.Black:XLColor.White;}catch{}
                    r++;
                    foreach(var sub in type.GroupBy(x=>x.SubType).OrderBy(x=>x.Key))
                    {
                        ws.Cell(r,1).Value="S_TYPE";ws.Cell(r,2).Value=sub.Key;ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                        var headerRow=r;var headers=new[]{"Date","A-Z","Détails","Type","S_Type","Débit","Crédit","Solde"};for(var i=0;i<headers.Length;i++)ws.Cell(r,i+1).Value=headers[i];ws.Range(r,1,r,8).Style.Font.Bold=true;r++;
                        decimal subtotalDebit=0,subtotalCredit=0;
                        var alternate=false;
                        foreach(var x in sub.OrderBy(x=>x.Op.Date).ThenBy(x=>x.Op.Id))
                        {
                            var debit=Math.Abs(Math.Min(0m,x.Op.Amount));var credit=Math.Max(0m,x.Op.Amount);subtotalDebit+=debit;subtotalCredit+=credit;
                            ws.Cell(r,1).Value=x.Op.Date;ws.Cell(r,2).Value=string.IsNullOrWhiteSpace(x.Op.InterbankLabel)?x.Op.Nature:x.Op.InterbankLabel;ws.Cell(r,3).Value=x.Op.Details;ws.Cell(r,4).Value=x.Type;ws.Cell(r,5).Value=x.SubType;ws.Cell(r,6).Value=debit;ws.Cell(r,7).Value=credit;ws.Cell(r,8).Value=credit-debit;
                            if(alternate)ws.Range(r,1,r,8).Style.Fill.BackgroundColor=XLColor.LightBlue;
                            if(debit>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;
                            if(credit>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;
                            ws.Cell(r,8).Style.Font.FontColor=(credit-debit)<0?XLColor.Red:XLColor.Green;
                            alternate=!alternate;r++;
                        }
                        ws.Cell(r,5).Value="Total";ws.Cell(r,6).Value=subtotalDebit;ws.Cell(r,7).Value=subtotalCredit;ws.Cell(r,8).Value=subtotalCredit-subtotalDebit;ws.Range(r,5,r,8).Style.Font.Bold=true;
                        if(subtotalDebit>0)ws.Cell(r,6).Style.Font.FontColor=XLColor.Red;
                        if(subtotalCredit>0)ws.Cell(r,7).Style.Font.FontColor=XLColor.Green;
                        ws.Cell(r,8).Style.Font.FontColor=(subtotalCredit-subtotalDebit)<0?XLColor.Red:XLColor.Green;r+=2;
                    }
                }
                r++;
            }
            r++;
        }
        ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(6,8).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);
        wb.SaveAs(save.FileName);_status.Text=$"Journal détaillé créé : {rows.Count:N0} opérations";
        try{Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show($"Le journal a bien été créé, mais son ouverture automatique a échoué.\n\n{ex.Message}","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        MessageBox.Show("Le journal détaillé a été créé et ouvert dans Excel.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private static Label Label(string s)=>new(){Text=s,AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(8,7,5,0)};

}
