using System.Diagnostics;
using System.Globalization;
using System.Drawing.Drawing2D;
using ClosedXML.Excel;

namespace QNB;

internal sealed class DataAnalysisForm : Form
{
    private readonly DateTimePicker _from=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly DateTimePicker _to=new(){Format=DateTimePickerFormat.Short,Width=120};
    private readonly Label _status=new(){AutoSize=true,ForeColor=Color.FromArgb(183,207,229)};

    public DataAnalysisForm()
    {
        Text="QNB - Analyse des données";StartPosition=FormStartPosition.CenterParent;Size=new Size(920,570);MinimumSize=new Size(760,520);
        BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var title=new Label{Text="ANALYSE DES DONNÉES",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(22,0,0,0),Font=new Font("Segoe UI Semibold",20F,FontStyle.Bold)};
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22,14,22,14),BackColor=Color.FromArgb(4,36,73),ColumnCount=1,RowCount=8};
        for(var i=0;i<8;i++)panel.RowStyles.Add(new RowStyle(SizeType.Absolute,i==0?45:49));
        var dates=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true};
        var ops=BankingRepository.LoadImports().SelectMany(x=>x.Operations).ToList();
        var min=ops.Count>0?ops.Min(x=>x.Date).Date:DateTime.Today;
        var max=ops.Count>0?ops.Max(x=>x.Date).Date:DateTime.Today;
        _from.Value=min;_to.Value=max;
        dates.Controls.Add(Label("Du"));dates.Controls.Add(_from);dates.Controls.Add(Label("au"));dates.Controls.Add(_to);
        panel.Controls.Add(dates,0,0);
        Button MakeButton(string text,int width,Color color,Action action)
        {
            var button=new Button{Text=text,Width=width,Height=36,Margin=new Padding(0,4,0,4),BackColor=color,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
            button.Click+=(_,_)=>action();return button;
        }
        panel.Controls.Add(MakeButton("Journal mensuel",180,Color.FromArgb(174,112,38),ExportJournal),0,1);
        panel.Controls.Add(MakeButton("Journal par Type",180,Color.FromArgb(16,112,187),ExportTypeJournal),0,2);
        panel.Controls.Add(MakeButton("Journal annuel / Poste",200,Color.FromArgb(92,82,160),ExportAnnualPostJournal),0,3);
        panel.Controls.Add(MakeButton("Camemberts dépenses / recettes",280,Color.FromArgb(36,133,127),ShowMonthlyPieCharts),0,4);
        panel.Controls.Add(MakeButton("Excel camemberts",200,Color.FromArgb(32,126,83),ExportPieChartsExcel),0,5);
        panel.Controls.Add(MakeButton("Journal par S-Type (période)",240,Color.FromArgb(91,77,168),ExportSubTypePeriodJournal),0,6);
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true};
        bottom.Controls.Add(MakeButton("Excel banques / postes / S-Type",290,Color.FromArgb(16,112,187),ExportBankSubTypeSummary));
        _status.Margin=new Padding(15,12,0,0);bottom.Controls.Add(_status);panel.Controls.Add(bottom,0,7);
        Controls.Add(panel);Controls.Add(title);
    }
    private static void ConfigurePrintLayout(XLWorkbook workbook)
    {
        foreach(var sheet in workbook.Worksheets)
        {
            sheet.PageSetup.PageOrientation=XLPageOrientation.Landscape;
            sheet.PageSetup.PagesWide=1;
            sheet.PageSetup.PagesTall=0;
            sheet.PageSetup.CenterHorizontally=true;
            sheet.PageSetup.Margins.Left=0.25;
            sheet.PageSetup.Margins.Right=0.25;
            sheet.PageSetup.Margins.Top=0.4;
            sheet.PageSetup.Margins.Bottom=0.4;
        }
    }

    private void ExportSubTypePeriodJournal()
    {
        var from=_from.Value.Date;var to=_to.Value.Date;
        var monthCount=(to.Year-from.Year)*12+to.Month-from.Month+1;
        if(from>to){MessageBox.Show("La date de début doit précéder la date de fin.","QNB");return;}
        var classifications=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports()
            .SelectMany(import=>import.Operations.Select(op=>new{Import=import,Op=op}))
            .Where(x=>x.Op.Date.Date>=from&&x.Op.Date.Date<=to)
            .Select(x=>{
                classifications.TryGetValue(x.Op.Id,out var cls);
                return new{
                    Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"Non renseignée":x.Import.BankName,
                    Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,
                    Post=string.IsNullOrWhiteSpace(cls?.Type)?"Non typé":cls.Type,
                    Sub=string.IsNullOrWhiteSpace(cls?.SubType)?"Non typé":cls.SubType,
                    Op=x.Op,Debit=Math.Abs(Math.Min(0m,x.Op.Amount)),Credit=Math.Max(0m,x.Op.Amount)
                };
            }).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur la période sélectionnée.","QNB");return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Journal_SType_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx"};
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Journal S-Type");
        ws.Cell(1,1).Value="JOURNAL PAR S-TYPE";
        ws.Range(1,1,1,10).Merge();ws.Range(1,1,1,9).Style.Font.Bold=true;ws.Range(1,1,1,9).Style.Font.FontSize=16;
        ws.Cell(2,1).Value="Du";ws.Cell(2,2).Value=from;ws.Cell(2,3).Value="au";ws.Cell(2,4).Value=to;
        ws.Cell(2,2).Style.DateFormat.Format="dd/MM/yyyy";ws.Cell(2,4).Style.DateFormat.Format="dd/MM/yyyy";
        var headers=new[]{"Banque","Compte bancaire","Poste","S-Type","Date","Nature","Libellé / Détails","Débit","Crédit","Solde / mois"};
        for(var i=0;i<headers.Length;i++)ws.Cell(4,i+1).Value=headers[i];
        ws.Range(4,1,4,10).Style.Font.Bold=true;ws.Range(4,1,4,9).Style.Fill.BackgroundColor=XLColor.FromHtml("#042449");ws.Range(4,1,4,9).Style.Font.FontColor=XLColor.White;
        var line=5;
        foreach(var sub in rows.GroupBy(x=>x.Sub).OrderBy(x=>x.Key))
        {
            foreach(var x in sub.OrderBy(x=>x.Op.Date).ThenBy(x=>x.Bank).ThenBy(x=>x.Account))
            {
                ws.Cell(line,1).Value=x.Bank;ws.Cell(line,2).Value=x.Account;ws.Cell(line,3).Value=x.Post;
                ws.Cell(line,4).Value=x.Sub;ws.Cell(line,5).Value=x.Op.Date;
                ws.Cell(line,6).Value=x.Op.Nature;
                ws.Cell(line,7).Value=string.IsNullOrWhiteSpace(x.Op.Details)?x.Op.InterbankLabel:x.Op.InterbankLabel+" - "+x.Op.Details;
                ws.Cell(line,8).Value=x.Debit;ws.Cell(line,9).Value=x.Credit;line++;
            }
            ws.Cell(line,4).Value="TOTAL S-TYPE : "+sub.Key;
            var subDebit=sub.Sum(x=>x.Debit);var subCredit=sub.Sum(x=>x.Credit);
            ws.Cell(line,8).Value=subDebit;ws.Cell(line,9).Value=subCredit;
            ws.Cell(line,10).Value=(subCredit-subDebit)/monthCount;
            ws.Range(line,1,line,10).Style.Font.Bold=true;ws.Range(line,1,line,9).Style.Fill.BackgroundColor=XLColor.FromHtml("#DCEBFA");line++;
        }
        ws.Cell(line,4).Value="TOTAL GÉNÉRAL";ws.Cell(line,8).Value=rows.Sum(x=>x.Debit);ws.Cell(line,9).Value=rows.Sum(x=>x.Credit);ws.Cell(line,10).Value=(rows.Sum(x=>x.Credit)-rows.Sum(x=>x.Debit))/monthCount;
        ws.Range(line,1,line,9).Style.Font.Bold=true;ws.Range(line,1,line,9).Style.Fill.BackgroundColor=XLColor.FromHtml("#84B8E5");
        ws.Range(5,5,line,5).Style.DateFormat.Format="dd/MM/yyyy";
        ws.Range(5,8,line,10).Style.NumberFormat.Format="#,##0.00";
        ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(4);
        ConfigurePrintLayout(wb);wb.SaveAs(save.FileName);_status.Text="Journal S-Type créé : "+Path.GetFileName(save.FileName);
        try{Process.Start(new ProcessStartInfo{FileName=save.FileName,UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show("Le fichier est enregistré, mais son ouverture a échoué : "+ex.Message,"QNB - Excel",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private void ExportBankSubTypeSummary()
    {
        var from=_from.Value.Date;var to=_to.Value.Date;
        if(from>to){MessageBox.Show("La date de début doit précéder la date de fin.","QNB");return;}
        var classifications=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports()
            .SelectMany(import=>import.Operations.Select(op=>new{Import=import,Op=op}))
            .Where(x=>x.Op.Date.Date>=from&&x.Op.Date.Date<=to)
            .Select(x=>{
                classifications.TryGetValue(x.Op.Id,out var classification);
                return new{
                    Bank=string.IsNullOrWhiteSpace(x.Import.BankName)?"Non renseignée":x.Import.BankName,
                    Account=string.IsNullOrWhiteSpace(x.Import.AccountDisplayName)?x.Import.AccountReference:x.Import.AccountDisplayName,
                    Post=string.IsNullOrWhiteSpace(classification?.Type)?"Non typé":classification.Type,
                    Sub=string.IsNullOrWhiteSpace(classification?.SubType)?"Non typé":classification.SubType,
                    Debit=Math.Abs(Math.Min(0m,x.Op.Amount)),Credit=Math.Max(0m,x.Op.Amount)
                };
            }).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur la période sélectionnée.","QNB");return;}
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Banques_Postes_SType_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx"};
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        using var workbook=new XLWorkbook();
        var sheet=workbook.Worksheets.Add("Banques et S-Type");
        sheet.Cell(1,1).Value="BANQUES / POSTES / S-TYPE";
        sheet.Range(1,1,1,8).Merge();
        sheet.Range(1,1,1,8).Style.Font.Bold=true;
        sheet.Range(1,1,1,8).Style.Font.FontSize=16;
        sheet.Cell(2,1).Value="Période du";sheet.Cell(2,2).Value=from;
        sheet.Cell(2,3).Value="au";sheet.Cell(2,4).Value=to;
        sheet.Cell(2,2).Style.DateFormat.Format="dd/MM/yyyy";
        sheet.Cell(2,4).Style.DateFormat.Format="dd/MM/yyyy";
        var headers=new[]{"Banque","Cpte bancaire","Poste","S-Type","Date début","Date fin","Débit","Crédit"};
        for(var i=0;i<headers.Length;i++)sheet.Cell(4,i+1).Value=headers[i];
        sheet.Range(4,1,4,8).Style.Font.Bold=true;
        sheet.Range(4,1,4,8).Style.Fill.BackgroundColor=XLColor.FromHtml("#042449");
        sheet.Range(4,1,4,8).Style.Font.FontColor=XLColor.White;
        var line=5;
        foreach(var bank in rows.GroupBy(x=>x.Bank).OrderBy(x=>x.Key))
        {
            foreach(var post in bank.GroupBy(x=>x.Post).OrderBy(x=>x.Key))
            {
                foreach(var sub in post.GroupBy(x=>new{x.Account,x.Sub}).OrderBy(x=>x.Key.Account).ThenBy(x=>x.Key.Sub))
                {
                    sheet.Cell(line,1).Value=bank.Key;sheet.Cell(line,2).Value=sub.Key.Account;
                    sheet.Cell(line,3).Value=post.Key;sheet.Cell(line,4).Value=sub.Key.Sub;
                    sheet.Cell(line,5).Value=from;sheet.Cell(line,6).Value=to;
                    sheet.Cell(line,7).Value=sub.Sum(x=>x.Debit);sheet.Cell(line,8).Value=sub.Sum(x=>x.Credit);
                    line++;
                }
                sheet.Cell(line,3).Value="TOTAL POSTE : "+post.Key;
                sheet.Cell(line,7).Value=post.Sum(x=>x.Debit);sheet.Cell(line,8).Value=post.Sum(x=>x.Credit);
                sheet.Range(line,1,line,8).Style.Font.Bold=true;
                sheet.Range(line,1,line,8).Style.Fill.BackgroundColor=XLColor.FromHtml("#DCEBFA");
                line++;
            }
            sheet.Cell(line,1).Value="TOTAL BANQUE : "+bank.Key;
            sheet.Cell(line,7).Value=bank.Sum(x=>x.Debit);sheet.Cell(line,8).Value=bank.Sum(x=>x.Credit);
            sheet.Range(line,1,line,8).Style.Font.Bold=true;
            sheet.Range(line,1,line,8).Style.Fill.BackgroundColor=XLColor.FromHtml("#BBD8F0");
            line++;
        }
        sheet.Cell(line,1).Value="TOTAL GÉNÉRAL";
        sheet.Cell(line,7).Value=rows.Sum(x=>x.Debit);sheet.Cell(line,8).Value=rows.Sum(x=>x.Credit);
        sheet.Range(line,1,line,8).Style.Font.Bold=true;
        sheet.Range(line,1,line,8).Style.Fill.BackgroundColor=XLColor.FromHtml("#84B8E5");
        sheet.Range(5,5,line,6).Style.DateFormat.Format="dd/MM/yyyy";
        sheet.Range(5,7,line,8).Style.NumberFormat.Format="#,##0.00";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(4);
        ConfigurePrintLayout(workbook);workbook.SaveAs(save.FileName);
        _status.Text="Export Excel créé : "+Path.GetFileName(save.FileName);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName=save.FileName,
                UseShellExecute=true
            });
        }
        catch(Exception ex)
        {
            MessageBox.Show("Le fichier a été enregistré, mais son ouverture automatique a échoué : "+ex.Message,
                "QNB - Excel",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
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
        ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(6,8).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);ConfigurePrintLayout(wb);wb.SaveAs(save.FileName);_status.Text=$"Journal annuel par Poste {year} : {rows.Count:N0} opérations";
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
        ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy";ws.Columns(6,8).Style.NumberFormat.Format="#,##0.00 €";ws.Columns().AdjustToContents();ws.SheetView.FreezeRows(1);ConfigurePrintLayout(wb);wb.SaveAs(save.FileName);_status.Text=$"Journal par Type créé : {rows.Count:N0} opérations";
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
        ConfigurePrintLayout(wb);wb.SaveAs(save.FileName);_status.Text=$"Journal détaillé créé : {rows.Count:N0} opérations";
        try{Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show($"Le journal a bien été créé, mais son ouverture automatique a échoué.\n\n{ex.Message}","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        MessageBox.Show("Le journal détaillé a été créé et ouvert dans Excel.","QNB - Analyse",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }



    private void ExportPieChartsExcel()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var classifications=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations)
            .Where(o=>o.Date.Date>=_from.Value.Date&&o.Date.Date<=_to.Value.Date)
            .Select(o=>{classifications.TryGetValue(o.Id,out var k);return new{Op=o,Type=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type};}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var expenses=rows.Where(x=>x.Op.Amount<0).GroupBy(x=>x.Type).Select(g=>new PieSlice(g.Key,g.Sum(x=>Math.Abs(x.Op.Amount)))).OrderByDescending(x=>x.Amount).ToList();
        var income=rows.Where(x=>x.Op.Amount>0).GroupBy(x=>x.Type).Select(g=>new PieSlice(g.Key,g.Sum(x=>x.Op.Amount))).OrderByDescending(x=>x.Amount).ToList();
        using var save=new SaveFileDialog{Filter="Classeur Excel (*.xlsx)|*.xlsx",FileName=$"QNB_Camemberts_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"};
        if(save.ShowDialog(this)!=DialogResult.OK)return;
        using(var workbook=new XLWorkbook())
        {
            AddPieDataSheet(workbook,"Dépenses",expenses);
            AddPieDataSheet(workbook,"Recettes",income);
            ConfigurePrintLayout(workbook);workbook.SaveAs(save.FileName);
        }
        // ClosedXML 0.104 ne crée pas de graphiques : Excel ajoute les deux vrais camemberts au classeur.
        object? excelObject=null;object? bookObject=null;
        try
        {
            var excelType=Type.GetTypeFromProgID("Excel.Application");
            if(excelType is null)throw new InvalidOperationException("Microsoft Excel doit être installé pour générer les graphiques.");
            excelObject=Activator.CreateInstance(excelType);
            if(excelObject is null)throw new InvalidOperationException("Impossible de démarrer Microsoft Excel.");
            dynamic excel=excelObject;
            excel.Visible=false;excel.DisplayAlerts=false;
            bookObject=excel.Workbooks.Open(Path.GetFullPath(save.FileName));
            dynamic book=bookObject;
            AddExcelPieChart(book,"Dépenses",expenses.Count,"Dépenses par Type");
            AddExcelPieChart(book,"Recettes",income.Count,"Recettes par Type");
            book.Save();book.Close(false);bookObject=null;
            excel.Quit();
            _status.Text="Camemberts Excel créés : "+Path.GetFileName(save.FileName);
            Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true});
        }
        catch(Exception ex)
        {
            MessageBox.Show("Les données Excel ont été enregistrées, mais les graphiques ou l'ouverture automatique ont échoué. Vérifiez que Microsoft Excel est installé.\\n\\n"+ex.Message,
                "QNB - Camemberts",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        finally
        {
            if(bookObject is not null)try{((dynamic)bookObject).Close(false);}catch{}
            if(excelObject is not null)try{((dynamic)excelObject).Quit();}catch{}
            if(bookObject is not null && System.Runtime.InteropServices.Marshal.IsComObject(bookObject))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(bookObject);
            if(excelObject is not null && System.Runtime.InteropServices.Marshal.IsComObject(excelObject))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(excelObject);
        }
    }

    private static void AddPieDataSheet(XLWorkbook workbook,string name,List<PieSlice> slices)
    {
        var ws=workbook.Worksheets.Add(name);
        ws.Cell(1,1).Value="Type";ws.Cell(1,2).Value="Montant (€)";
        ws.Range(1,1,1,2).Style.Font.Bold=true;
        for(var i=0;i<slices.Count;i++){ws.Cell(i+2,1).Value=slices[i].Name;ws.Cell(i+2,2).Value=slices[i].Amount;}
        var totalRow=slices.Count+2;ws.Cell(totalRow,1).Value="TOTAL";ws.Cell(totalRow,2).Value=slices.Sum(x=>x.Amount);
        ws.Range(totalRow,1,totalRow,2).Style.Font.Bold=true;
        ws.Column(2).Style.NumberFormat.Format="#,##0.00 €";ws.Columns(1,2).AdjustToContents();
    }

    private static void AddExcelPieChart(dynamic book,string sheetName,int count,string title)
    {
        if(count==0)return;
        dynamic sheet=book.Worksheets[sheetName];
        dynamic chartObject=sheet.ChartObjects().Add(370,30,600,390);
        dynamic chart=chartObject.Chart;
        chart.ChartType=5; // xlPie
        chart.SetSourceData(sheet.Range[sheet.Cells[1,1],sheet.Cells[count+1,2]]);
        chart.HasTitle=true;chart.ChartTitle.Text=title;
        chart.HasLegend=true;
        dynamic series=chart.SeriesCollection(1);
        series.ApplyDataLabels();
    }

    private void ShowMonthlyPieCharts()
    {
        if(_from.Value.Date>_to.Value.Date){MessageBox.Show("La date de début doit être antérieure à la date de fin.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        var cls=BankingRepository.LoadOperationClassifications();
        var rows=BankingRepository.LoadImports().SelectMany(i=>i.Operations)
            .Where(o=>o.Date.Date>=_from.Value.Date&&o.Date.Date<=_to.Value.Date)
            .Select(o=>{cls.TryGetValue(o.Id,out var k);return new{Op=o,Type=string.IsNullOrWhiteSpace(k?.Type)?"Non typé":k.Type};}).ToList();
        if(rows.Count==0){MessageBox.Show("Aucune opération sur cette période.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var expenses=rows.Where(x=>x.Op.Amount<0).GroupBy(x=>x.Type)
            .Select(g=>new PieSlice(g.Key,g.Sum(x=>Math.Abs(x.Op.Amount)))).OrderByDescending(x=>x.Amount).ToList();
        var income=rows.Where(x=>x.Op.Amount>0).GroupBy(x=>x.Type)
            .Select(g=>new PieSlice(g.Key,g.Sum(x=>x.Op.Amount))).OrderByDescending(x=>x.Amount).ToList();
        using var dialog=new Form{Text=$"QNB - Dépenses et recettes du {_from.Value:dd/MM/yyyy} au {_to.Value:dd/MM/yyyy}",
            StartPosition=FormStartPosition.CenterParent,Size=new Size(1200,740),MinimumSize=new Size(900,560),
            BackColor=Color.FromArgb(3,23,49),ForeColor=Color.White,Font=new Font("Segoe UI",10F)};
        var tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(16,7)};
        foreach(var (name,data) in new[]{("Dépenses",expenses),("Recettes",income)})
        {
            var page=new TabPage(name){BackColor=Color.White,ForeColor=Color.FromArgb(3,23,49)};
            var chart=new PieChartPanel(data,name){Dock=DockStyle.Fill};
            page.Controls.Add(chart);tabs.TabPages.Add(page);
        }
        dialog.Controls.Add(tabs);dialog.ShowDialog(this);
    }

    private sealed record PieSlice(string Name,decimal Amount);

    private sealed class PieChartPanel : Panel
    {
        private static readonly Color[] Palette={
            Color.FromArgb(32,116,190),Color.FromArgb(225,123,48),Color.FromArgb(48,154,112),
            Color.FromArgb(145,99,190),Color.FromArgb(221,75,102),Color.FromArgb(61,170,180),
            Color.FromArgb(210,169,49),Color.FromArgb(95,110,140)};
        private readonly List<PieSlice> _slices;
        private readonly string _title;
        public PieChartPanel(List<PieSlice> slices,string title)
        {
            _slices=slices;_title=title;DoubleBuffered=true;AutoScroll=false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using var heading=new Font("Segoe UI Semibold",17F,FontStyle.Bold);
            using var body=new Font("Segoe UI",10F);
            using var small=new Font("Segoe UI",9F);
            using var ink=new SolidBrush(Color.FromArgb(3,35,68));
            var total=_slices.Sum(x=>x.Amount);
            g.DrawString($"{_title} par Type",heading,ink,25,18);
            g.DrawString($"Total : {total.ToString("N2",CultureInfo.GetCultureInfo("fr-FR"))} €",body,ink,27,58);
            if(total<=0){g.DrawString("Aucune opération correspondante.",body,ink,30,115);return;}
            var diameter=Math.Max(170,Math.Min(Math.Min(ClientSize.Width*0.48f,ClientSize.Height-155),440));
            var pie=new RectangleF(30,105,diameter,diameter);
            var start=-90f;
            for(var i=0;i<_slices.Count;i++)
            {
                var angle=(float)(_slices[i].Amount/total*360m);
                using var brush=new SolidBrush(Palette[i%Palette.Length]);
                if(i==_slices.Count-1)angle=270f-start;
                g.FillPie(brush,pie.X,pie.Y,pie.Width,pie.Height,start,angle);
                start+=angle;
            }
            var legendX=pie.Right+28f;
            var legendY=105f;
            var available=Math.Max(170,ClientSize.Width-legendX-20);
            for(var i=0;i<_slices.Count;i++)
            {
                var y=legendY+i*34f;
                if(y>ClientSize.Height-24){g.DrawString($"… {_slices.Count-i} catégorie(s) supplémentaires",small,ink,legendX,y-5);break;}
                using var brush=new SolidBrush(Palette[i%Palette.Length]);
                g.FillRectangle(brush,legendX,y+3,14,14);
                var percent=_slices[i].Amount/total*100m;
                var label=$"{_slices[i].Name} : {_slices[i].Amount:N2} € ({percent:N1} %)";
                var bounds=new RectangleF(legendX+22,y-2,available-22,32);
                g.DrawString(label,small,ink,bounds);
            }
        }
    }

    private static Label Label(string s)=>new(){Text=s,AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(8,7,5,0)};

}
