namespace QNB;

internal sealed class ManualOperationForm : Form
{
    private readonly ComboBox _account=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=390};
    private readonly DateTimePicker _date=new(){Format=DateTimePickerFormat.Short,Width=130};
    private readonly TextBox _nature=new(){Width=390};
    private readonly NumericUpDown _debit=new(){Width=140,DecimalPlaces=2,Maximum=100000000,ThousandsSeparator=true};
    private readonly NumericUpDown _credit=new(){Width=140,DecimalPlaces=2,Maximum=100000000,ThousandsSeparator=true};
    private readonly TextBox _label=new(){Width=390};
    private readonly TextBox _details=new(){Width=390,Height=65,Multiline=true};
    private readonly List<BankImportResult> _imports;
    public BankImportResult SelectedImport => _imports[_account.SelectedIndex];
    public BankOperation Operation => new(){Date=_date.Value.Date,Nature=_nature.Text.Trim(),Debit=-Math.Abs(_debit.Value),Credit=Math.Abs(_credit.Value),Currency="EUR",InterbankLabel=_label.Text.Trim(),Details=_details.Text.Trim()};

    public ManualOperationForm(List<BankImportResult> imports,(BankImportResult Import,BankOperation Operation)? copy)
    {
        _imports=imports;Text=copy is null?"QNB - Ajouter une opération":"QNB - Dupliquer une opération";StartPosition=FormStartPosition.CenterParent;Size=new Size(560,450);MinimumSize=new Size(560,450);BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=2,RowCount=8,BackColor=BackColor};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var i in imports)_account.Items.Add($"{i.BankName} - {(string.IsNullOrWhiteSpace(i.AccountDisplayName)?i.AccountReference:i.AccountDisplayName)}");
        _account.SelectedIndex=0;
        Add(panel,0,"Compte",_account);Add(panel,1,"Date",_date);Add(panel,2,"Nature",_nature);Add(panel,3,"Débit",_debit);Add(panel,4,"Crédit",_credit);Add(panel,5,"Libellé",_label);Add(panel,6,"Détails",_details);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
        var ok=new Button{Text="Valider",Width=100,Height=34,BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.OK};
        var cancel=new Button{Text="Annuler",Width=100,Height=34,BackColor=Color.FromArgb(70,82,100),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);panel.Controls.Add(buttons,1,7);AcceptButton=ok;CancelButton=cancel;
        if(copy is not null){var x=copy.Value;var idx=imports.FindIndex(i=>ReferenceEquals(i,x.Import)||i.Operations.Any(o=>o.Id==x.Operation.Id));if(idx>=0)_account.SelectedIndex=idx;_date.Value=x.Operation.Date;_nature.Text=x.Operation.Nature;_debit.Value=Math.Min(_debit.Maximum,Math.Abs(Math.Min(0,x.Operation.Debit)));_credit.Value=Math.Min(_credit.Maximum,Math.Abs(Math.Max(0,x.Operation.Credit)));_label.Text=x.Operation.InterbankLabel;_details.Text=x.Operation.Details;}
        Controls.Add(panel);
    }
    private static void Add(TableLayoutPanel p,int row,string text,Control control){p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=text,AutoSize=true,ForeColor=Color.FromArgb(183,207,229),Margin=new Padding(0,7,5,0)},0,row);p.Controls.Add(control,1,row);}
}
