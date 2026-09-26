namespace NTFSSuite;
public sealed class AnalyzerPanel:UserControl
{
 TextBox template=new(),root=new(),output=new(),log=new();NumericUpDown levels=new();Button run=new();
 public AnalyzerPanel(){Dock=DockStyle.Fill;int y=20;Controls.Add(new Label{Text="Analizador de combinaciones ACL",Left=20,Top=y,AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold)});y+=50;Ui.Line(this,"Plantilla",template,ref y,(s,e)=>template.Text=Ui.PickFile());Ui.Line(this,"Raíz NTFS",root,ref y,(s,e)=>root.Text=Ui.PickFolder());Ui.Line(this,"Reporte",output,ref y,(s,e)=>output.Text=Ui.PickSave("Analisis_Combinaciones_ACL.xlsx"));AddLevels(ref y);run.Text="Analizar combinaciones";run.SetBounds(215,y,260,38);run.Click+=Run;Controls.Add(run);y+=52;log.SetBounds(20,y,865,120);log.Multiline=true;log.ReadOnly=true;Controls.Add(log);}
 void AddLevels(ref int y){Controls.Add(new Label{Text="Niveles",Left=20,Top=y+5,Width=190});levels.SetBounds(215,y,80,27);levels.Minimum=1;levels.Maximum=10;levels.Value=5;Controls.Add(levels);y+=42;}
 async void Run(object? s,EventArgs e){if(!File.Exists(template.Text)||!Directory.Exists(root.Text)||output.Text.Length==0){MessageBox.Show("Complete los campos.");return;}run.Enabled=false;try{await Task.Run(()=>AclPatternAnalyzer.Run(template.Text,root.Text,(int)levels.Value,output.Text,(p,m)=>BeginInvoke(()=>log.Text=$"{p}% {m}")));log.Text="Reporte generado: "+output.Text;MessageBox.Show("Análisis terminado.");}catch(Exception ex){log.Text=ex.ToString();}finally{run.Enabled=true;}}
}
public sealed class ComparatorPanel:UserControl
{
 TextBox client=new(),scan=new(),output=new();NumericUpDown levels=new();
 public ComparatorPanel(){Dock=DockStyle.Fill;int y=20;Controls.Add(new Label{Text="Comparador de matrices",Left=20,Top=y,AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold)});y+=50;Ui.Line(this,"Matriz cliente",client,ref y,(s,e)=>client.Text=Ui.PickFile());Ui.Line(this,"Matriz escaneada",scan,ref y,(s,e)=>scan.Text=Ui.PickFile());Ui.Line(this,"Reporte",output,ref y,(s,e)=>output.Text=Ui.PickSave("Comparacion_Matrices.xlsx"));Controls.Add(new Label{Text="Niveles",Left=20,Top=y+5,Width=190});levels.SetBounds(215,y,80,27);levels.Minimum=1;levels.Maximum=10;levels.Value=5;Controls.Add(levels);y+=45;var b=new Button{Text="Comparar",Left=215,Top=y,Width=220,Height=38};b.Click+=(s,e)=>{try{MatrixComparator.Run(client.Text,scan.Text,output.Text,(int)levels.Value);MessageBox.Show("Comparación generada.");}catch(Exception ex){MessageBox.Show(ex.Message);}};Controls.Add(b);}
}
public sealed class PlannerPanel:UserControl
{
 TextBox matrix=new(),root=new(),output=new();NumericUpDown levels=new();
 public PlannerPanel(){Dock=DockStyle.Fill;int y=20;Controls.Add(new Label{Text="Planeador de cambios amarillos",Left=20,Top=y,AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold)});y+=50;Ui.Line(this,"Matriz cliente",matrix,ref y,(s,e)=>matrix.Text=Ui.PickFile());Ui.Line(this,"Raíz NTFS",root,ref y,(s,e)=>root.Text=Ui.PickFolder());Ui.Line(this,"Plan",output,ref y,(s,e)=>output.Text=Ui.PickSave("Plan_Cambios_NTFS.xlsx"));Controls.Add(new Label{Text="Niveles",Left=20,Top=y+5,Width=190});levels.SetBounds(215,y,80,27);levels.Minimum=1;levels.Maximum=10;levels.Value=5;Controls.Add(levels);y+=45;var b=new Button{Text="Generar plan (sin aplicar)",Left=215,Top=y,Width=260,Height=38};b.Click+=(s,e)=>{try{ChangePlanner.Run(matrix.Text,root.Text,output.Text,(int)levels.Value);MessageBox.Show("Plan generado. No se modificó NTFS.");}catch(Exception ex){MessageBox.Show(ex.Message);}};Controls.Add(b);}
}
public sealed class PlaceholderPanel:UserControl
{
 public PlaceholderPanel(string title,string text){Dock=DockStyle.Fill;Controls.Add(new Label{Text=title,Left=30,Top=35,AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold)});Controls.Add(new Label{Text=text,Left=30,Top=90,Width=800,Height=180,Font=new Font("Segoe UI",11)});}
}