namespace QNB;

internal sealed class AccountManagementForm : Form
{
    private readonly ListBox _accounts=new(){Dock=DockStyle.Fill,IntegralHeight=false};
    private List<BankAccountProfile> _items=new();
    private BankAccountProfile? Selected => _accounts.SelectedIndex>=0 && _accounts.SelectedIndex<_items.Count?_items[_accounts.SelectedIndex]:null;
    public AccountManagementForm()
    {
        Text="QNB - Listes - Gestion des banques et comptes";StartPosition=FormStartPosition.CenterParent;
        Size=new Size(860,570);MinimumSize=new Size(750,480);BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",10F);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=3};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,75));
        layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Banques et comptes enregistrés — sélectionnez une ligne pour la modifier ou la fusionner.\nLes opérations et relevés sont conservés.",ForeColor=Color.White},0,0);
        _accounts.BackColor=Color.FromArgb(15,45,78);_accounts.ForeColor=Color.White;layout.Controls.Add(_accounts,0,1);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};
        Button Add(string name,Action action){var b=new Button{Text=name,Width=150,Height=38,BackColor=Color.FromArgb(16,112,187),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};b.Click+=(_,_)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"QNB",MessageBoxButtons.OK,MessageBoxIcon.Error);}};actions.Controls.Add(b);return b;}
        Add("Modifier",Edit);Add("Fusionner / remplacer",Merge);Add("Supprimer si vide",Delete);Add("Actualiser",RefreshList);
        layout.Controls.Add(actions,0,2);Controls.Add(layout);RefreshList();
    }
    private void RefreshList()
    {
        var previous=Selected?.Id;_items=BankingRepository.LoadConfiguration().Accounts;
        _accounts.BeginUpdate();_accounts.Items.Clear();
        foreach(var a in _items)_accounts.Items.Add(a.BankName+"  |  "+a.AccountName+"  |  "+a.AccountReference+"  |  "+(a.SourceDate?.ToString("dd/MM/yyyy")??"sans date")+"  |  "+BankingRepository.CountAccountOperations(a.Id)+" opération(s)");
        _accounts.EndUpdate();var index=_items.FindIndex(x=>x.Id==previous);if(index>=0)_accounts.SelectedIndex=index;
    }
    private void Edit()
    {
        var a=Selected;if(a is null){MessageBox.Show(this,"Sélectionnez un compte.");return;}
        using var d=new Form{Text="Modifier la banque et le compte",StartPosition=FormStartPosition.CenterParent,Size=new Size(550,410),BackColor=BackColor,ForeColor=Color.White,Font=Font};
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(15),ColumnCount=2,RowCount=7};
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        TextBox Field(int n,string title,string value){var label=new Label{Text=title,AutoSize=true,ForeColor=Color.White};var box=new TextBox{Text=value,Dock=DockStyle.Fill};p.Controls.Add(label,0,n);p.Controls.Add(box,1,n);return box;}
        var bank=Field(0,"Banque",a.BankName);var name=Field(1,"Nom du compte",a.AccountName);var reference=Field(2,"Référence / IBAN",a.AccountReference);var holder=Field(3,"Titulaire",a.Holder);
        var type=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};type.Items.AddRange(Enum.GetNames(typeof(BankAccountType)));type.SelectedIndex=(int)a.Type;p.Controls.Add(new Label{Text="Type",ForeColor=Color.White},0,4);p.Controls.Add(type,1,4);
        var date=new DateTimePicker{Dock=DockStyle.Fill,Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=a.SourceDate.HasValue,Value=a.SourceDate??DateTime.Today};p.Controls.Add(new Label{Text="Date source",ForeColor=Color.White},0,5);p.Controls.Add(date,1,5);
        var ok=new Button{Text="Enregistrer",DialogResult=DialogResult.OK,BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,Width=140};p.Controls.Add(ok,1,6);d.AcceptButton=ok;d.Controls.Add(p);
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        a.BankName=bank.Text.Trim();a.AccountName=name.Text.Trim();a.AccountReference=reference.Text.Trim();a.Holder=holder.Text.Trim();a.Type=(BankAccountType)type.SelectedIndex;a.SourceDate=date.Checked?date.Value.Date:null;
        BankingRepository.EditAccount(a);RefreshList();
    }
    private void Merge()
    {
        var source=Selected;if(source is null){MessageBox.Show(this,"Sélectionnez le compte à remplacer.");return;}
        var destinations=_items.Where(x=>x.Id!=source.Id).ToList();if(destinations.Count==0){MessageBox.Show(this,"Créez d'abord un autre compte de destination.");return;}
        using var d=new Form{Text="Choisir le compte à conserver",StartPosition=FormStartPosition.CenterParent,Size=new Size(590,250),BackColor=BackColor,ForeColor=Color.White,Font=Font};
        var info=new Label{Text="Compte remplacé : "+source.DisplayName+"\nToutes ses opérations et ses relevés seront rattachés au compte choisi :",Left=18,Top=16,Width=540,Height=65,ForeColor=Color.White};
        var choice=new ComboBox{Left=18,Top=85,Width=540,DropDownStyle=ComboBoxStyle.DropDownList};
        foreach(var x in destinations)choice.Items.Add(x.DisplayName+" — "+x.AccountReference);choice.SelectedIndex=0;
        var ok=new Button{Text="Continuer",Left=410,Top=130,Width=145,Height=36,DialogResult=DialogResult.OK,BackColor=Color.FromArgb(16,112,187),ForeColor=Color.White};
        d.Controls.Add(info);d.Controls.Add(choice);d.Controls.Add(ok);d.AcceptButton=ok;
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        var target=destinations[choice.SelectedIndex];
        if(MessageBox.Show(this,"Remplacer « "+source.DisplayName+" » par « "+target.DisplayName+" » ?\n\nLes opérations et relevés seront conservés. Cette action ne supprime aucune opération.","Confirmer la fusion",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        BankingRepository.MergeAccounts(source.Id,target.Id);RefreshList();
    }
    private void Delete()
    {
        var a=Selected;if(a is null){MessageBox.Show(this,"Sélectionnez un compte.");return;}
        if(MessageBox.Show(this,"Supprimer le compte « "+a.DisplayName+" » uniquement s'il ne possède aucun relevé ?\nAucune opération ne sera supprimée.","Confirmer",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        BankingRepository.DeleteEmptyAccount(a.Id);RefreshList();
    }
}
