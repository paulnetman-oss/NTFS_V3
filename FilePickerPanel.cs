namespace NTFSSuite;
public static class Ui
{
 public static void Line(Control parent,string label,TextBox box,ref int y,EventHandler click,bool folder=false){parent.Controls.Add(new Label{Text=label,Left=20,Top=y+5,Width=190});box.SetBounds(215,y,570,27);parent.Controls.Add(box);var b=new Button{Text="Examinar",Left=795,Top=y-1,Width=90};b.Click+=click;parent.Controls.Add(b);y+=42;}
 public static string PickFile(string filter="Excel (*.xlsx)|*.xlsx"){using var d=new OpenFileDialog{Filter=filter};return d.ShowDialog()==DialogResult.OK?d.FileName:"";}
 public static string PickSave(string name){using var d=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName=name,DefaultExt="xlsx"};return d.ShowDialog()==DialogResult.OK?d.FileName:"";}
 public static string PickFolder(){using var d=new FolderBrowserDialog();return d.ShowDialog()==DialogResult.OK?d.SelectedPath:"";}
}