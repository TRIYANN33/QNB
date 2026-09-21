namespace QNB;

internal sealed class ClassificationRulesForm : Form
{
    private readonly DataGridView _grid = new();
    private List<ClassificationRule> _rules = new();

    public ClassificationRulesForm()
    {
        Text="QNB - Règles de typage automatique"; StartPosition=FormStartPosition.CenterParent;
        Size=new Size(1050,650); MinimumSize=new Size(850,520); BackColor=Color.FromArgb(3,23,49); ForeColor=Color.White; Font=new Font("Segoe UI",9.3F);

        var title=new Label{Text="RÈGLES DE TYPAGE",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(18,0,0,0),Font=new Font("Segoe UI Semibold",20F,FontStyle.Bold),ForeColor=Color.White};
        var info=new Label{Text="Le texte est recherché dans Nature + Libellé + Détails. La priorité la plus faible est appliquée en premier.",Dock=DockStyle.Top,Height=34,Padding=new Padding(20,0,0,0),ForeColor=Color.FromArgb(183,207,229)};

        _grid.Dock=DockStyle.Fill; _grid.BackgroundColor=Color.FromArgb(7,43,82); _grid.ForeColor=Color.White; _grid.AutoGenerateColumns=false;
        _grid.AllowUserToAddRows=false; _grid.AllowUserToDeleteRows=false; _grid.ReadOnly=true; _grid.RowHeadersVisible=false; _grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect=false;
        _grid.EnableHeadersVisualStyles=false; _grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(8,73,137); _grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        _grid.DefaultCellStyle.BackColor=Color.FromArgb(7,43,82); _grid.DefaultCellStyle.ForeColor=Color.White; _grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(18,82,146);
        AddColumn("Actif","Enabled",60); AddColumn("Priorité","Priority",70); AddColumn("Si le texte contient","ContainsText",220); AddColumn("Type","Type",160); AddColumn("S_Type","SubType",180); AddColumn("Couleur","CellColor",110);
        _grid.CellFormatting += FormatColorCell;

        var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=62,Padding=new Padding(12),BackColor=Color.FromArgb(4,36,73)};
        var add=Button("Ajouter",Color.FromArgb(25,130,105),110); var edit=Button("Modifier",Color.FromArgb(34,149,255),110);
        var del=Button("Supprimer",Color.FromArgb(190,48,58),110); var apply=Button("Appliquer maintenant",Color.FromArgb(108,76,170),165); var close=Button("Fermer",Color.FromArgb(60,75,95),100);
        add.Click+=(_,_)=>EditRule(null); edit.Click+=(_,_)=>EditSelected(); del.Click+=(_,_)=>DeleteSelected(); apply.Click+=(_,_)=>ApplyRules(); close.Click+=(_,_)=>Close();
        bar.Controls.AddRange(new Control[]{add,edit,del,apply,close});
        Controls.Add(_grid); Controls.Add(info); Controls.Add(title); Controls.Add(bar); Shown+=(_,_)=>Reload();
    }

    private void FormatColorCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if(e.RowIndex<0 || _grid.Columns[e.ColumnIndex].DataPropertyName!="CellColor") return;
        var value=Convert.ToString(e.Value);
        if(string.IsNullOrWhiteSpace(value)) return;
        try
        {
            var color=ColorTranslator.FromHtml(value);
            e.CellStyle.BackColor=color;
            e.CellStyle.ForeColor=(color.R*299+color.G*587+color.B*114)/1000>140?Color.Black:Color.White;
            e.CellStyle.SelectionBackColor=color;
            e.CellStyle.SelectionForeColor=e.CellStyle.ForeColor;
        }
        catch { }
    }

    private static Button Button(string text,Color color,int width)=>new(){Text=text,Width=width,Height=34,BackColor=color,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Margin=new Padding(6,0,6,0)};
    private void AddColumn(string header,string property,int width)=>_grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=header,DataPropertyName=property,Width=width,AutoSizeMode=property=="SubType"?DataGridViewAutoSizeColumnMode.Fill:DataGridViewAutoSizeColumnMode.None});
    private void Reload(){_rules=BankingRepository.LoadClassificationRules();_grid.DataSource=null;_grid.DataSource=_rules;}
    private ClassificationRule? Selected()=>_grid.CurrentRow?.DataBoundItem as ClassificationRule;
    private void EditSelected(){var rule=Selected();if(rule is null){MessageBox.Show("Sélectionnez une règle.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}EditRule(rule);}
    private void EditRule(ClassificationRule? source)
    {
        using var form=new ClassificationRuleEditForm(source);if(form.ShowDialog(this)!=DialogResult.OK)return;BankingRepository.SaveClassificationRule(form.Rule);Reload();
    }
    private void DeleteSelected()
    {
        var rule=Selected();if(rule is null)return;
        if(MessageBox.Show($"Supprimer la règle « {rule.ContainsText} » ?","QNB - Confirmation",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        BankingRepository.DeleteClassificationRule(rule.Id);Reload();
    }
    private void ApplyRules()
    {
        var count=BankingRepository.ApplyAutomaticClassification();
        MessageBox.Show($"{count} opération(s) classée(s).\n\nLes typages manuels ont été conservés.","QNB - Règles auto",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
}

internal sealed class ClassificationRuleEditForm : Form
{
    private readonly TextBox _contains=new(),_type=new(),_subType=new(),_color=new(); private readonly Button _colorButton=new(); private readonly NumericUpDown _priority=new(); private readonly CheckBox _enabled=new();
    private readonly long _id;
    public ClassificationRule Rule=>new(){Id=_id,ContainsText=_contains.Text.Trim(),Type=_type.Text.Trim(),SubType=_subType.Text.Trim(),Priority=(int)_priority.Value,Enabled=_enabled.Checked,CellColor=_color.Text};

    public ClassificationRuleEditForm(ClassificationRule? rule)
    {
        _id=rule?.Id??0; Text=rule is null?"QNB - Nouvelle règle":"QNB - Modifier la règle";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(520,385);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Color.FromArgb(3,23,49);ForeColor=Color.White;Font=new Font("Segoe UI",9.5F);
        AddLabel("Si le texte contient",25,30);_contains.SetBounds(180,26,305,28);_contains.Text=rule?.ContainsText??"";Controls.Add(_contains);
        AddLabel("Type",25,82);_type.SetBounds(180,78,305,28);_type.Text=rule?.Type??"";Controls.Add(_type);
        AddLabel("S_Type",25,134);_subType.SetBounds(180,130,305,28);_subType.Text=rule?.SubType??"";Controls.Add(_subType);
        AddLabel("Couleur cellule",25,186);_color.SetBounds(180,182,190,28);_color.ReadOnly=true;_color.Text=rule?.CellColor??"";Controls.Add(_color);ApplyColorPreview();
        _colorButton.Text="Choisir...";_colorButton.SetBounds(380,180,105,32);_colorButton.Click+=(_,_)=>ChooseColor();Controls.Add(_colorButton);
        AddLabel("Priorité",25,228);_priority.SetBounds(180,224,100,28);_priority.Minimum=1;_priority.Maximum=9999;_priority.Value=rule?.Priority??100;Controls.Add(_priority);
        _enabled.Text="Règle active";_enabled.SetBounds(310,226,150,28);_enabled.Checked=rule?.Enabled??true;_enabled.ForeColor=Color.White;Controls.Add(_enabled);
        var save=new Button{Text="Enregistrer",Left=340,Top=305,Width=145,Height=36,BackColor=Color.FromArgb(25,130,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var cancel=new Button{Text="Annuler",Left=210,Top=250,Width=115,Height=36,DialogResult=DialogResult.Cancel};save.Click+=(_,_)=>Save();Controls.Add(save);Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;
    }
    private void ChooseColor()
    {
        using var picker=new ColorDialog{FullOpen=true};
        if(!string.IsNullOrWhiteSpace(_color.Text)){try{picker.Color=ColorTranslator.FromHtml(_color.Text);}catch{}}
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        _color.Text=ColorTranslator.ToHtml(picker.Color);ApplyColorPreview();
    }
    private void ApplyColorPreview()
    {
        if(string.IsNullOrWhiteSpace(_color.Text)){_color.BackColor=SystemColors.Window;_color.ForeColor=SystemColors.WindowText;return;}
        try{var color=ColorTranslator.FromHtml(_color.Text);_color.BackColor=color;_color.ForeColor=(color.R*299+color.G*587+color.B*114)/1000>140?Color.Black:Color.White;}catch{}
    }
    private void AddLabel(string text,int x,int y)=>Controls.Add(new Label{Text=text,AutoSize=true,Location=new Point(x,y),ForeColor=Color.FromArgb(183,207,229)});
    private void Save(){if(string.IsNullOrWhiteSpace(_contains.Text)||string.IsNullOrWhiteSpace(_type.Text)){MessageBox.Show("Le texte recherché et le Type sont obligatoires.","QNB",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}DialogResult=DialogResult.OK;Close();}
}
