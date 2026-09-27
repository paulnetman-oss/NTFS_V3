namespace NTFSSuite;

public sealed class SidAnalyzerPanel : UserControl
{
    private readonly TextBox template=new(),root=new(),output=new(),log=new();
    private readonly NumericUpDown levels=new(){Minimum=1,Maximum=10,Value=5};
    private readonly ProgressBar progress=new();
    private readonly Button run=new(){Text="Analizar SID",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(14,4,14,4),MaximumSize=new Size(180,34)};

    public SidAnalyzerPanel()
    {
        Dock=DockStyle.Fill;Padding=new Padding(22,18,22,18);AutoScroll=true;
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,MinimumSize=new Size(650,0)};Controls.Add(layout);
        layout.Controls.Add(new Label{Text="Investigación forense de SID",AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold),Margin=new Padding(0,0,0,18)});
        AddPicker(layout,"Plantilla",template,()=>template.Text=Ui.PickFile());
        AddPicker(layout,"Raíz NTFS",root,()=>root.Text=Ui.PickFolder());
        AddPicker(layout,"Libro de salida",output,()=>output.Text=Ui.PickSave("Analisis_Origen_SID.xlsx"));
        var levelLine=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.LeftToRight,Margin=new Padding(0,4,0,4)};levelLine.Controls.Add(new Label{Text="Niveles",Width=110,TextAlign=ContentAlignment.MiddleLeft,Height=28});levels.Width=80;levelLine.Controls.Add(levels);layout.Controls.Add(levelLine);
        var actionLine=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(110,3,0,3)};run.Click+=Run;actionLine.Controls.Add(run);layout.Controls.Add(actionLine);
        progress.Dock=DockStyle.Top;progress.Height=20;layout.Controls.Add(progress);
        log.Multiline=true;log.ScrollBars=ScrollBars.Both;log.ReadOnly=true;log.MinimumSize=new Size(600,130);log.Dock=DockStyle.Top;layout.Controls.Add(log);
    }

    private static void AddPicker(TableLayoutPanel parent,string caption,TextBox box,Action action)
    {
        var row=new TableLayoutPanel{Dock=DockStyle.Top,Height=40,ColumnCount=3,Margin=new Padding(0)};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));
        row.Controls.Add(new Label{Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);box.Dock=DockStyle.Fill;box.Margin=new Padding(0,5,8,5);row.Controls.Add(box,1,0);var b=new Button{Text="Examinar",Dock=DockStyle.Fill,Margin=new Padding(0,3,0,3)};b.Click+=(_,_)=>action();row.Controls.Add(b,2,0);parent.Controls.Add(row);
    }

    private async void Run(object? sender,EventArgs e)
    {
        if(!File.Exists(template.Text)||!Directory.Exists(root.Text)||string.IsNullOrWhiteSpace(output.Text)){MessageBox.Show("Complete plantilla, raíz y salida.");return;}
        run.Enabled=false;progress.Value=0;
        try{await Task.Run(()=>SidOriginAnalyzer.Run(template.Text,root.Text,(int)levels.Value,output.Text,(p,m)=>BeginInvoke(()=>{progress.Value=Math.Clamp(p,0,100);log.Text=$"{p}%  {m}";})));log.Text="Libro generado: "+output.Text;MessageBox.Show("Investigación finalizada. No se modificó ninguna ACL.");}
        catch(Exception ex){log.Text=ex.ToString();MessageBox.Show(ex.Message,"Error");}
        finally{run.Enabled=true;}
    }
}
