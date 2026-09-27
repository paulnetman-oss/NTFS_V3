namespace NTFSSuite;

public abstract class ModulePanelBase : UserControl
{
    protected readonly TableLayoutPanel LayoutPanel;

    protected ModulePanelBase(string title)
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(22, 18, 22, 18);

        LayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0),
            MinimumSize = new Size(650, 0)
        };
        Controls.Add(LayoutPanel);

        var heading = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 22)
        };
        AddRow(heading, 48);
    }

    protected void AddRow(Control control, int height = 42)
    {
        int row = LayoutPanel.RowCount++;
        LayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        control.Dock = DockStyle.Fill;
        LayoutPanel.Controls.Add(control, 0, row);
    }

    protected void AddPicker(
        string label,
        TextBox box,
        EventHandler action)
    {
        var host = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        Ui.AddPickerLine(host, label, box, 0, action);
        AddRow(host, 42);
    }

    protected NumericUpDown AddLevels()
    {
        var line = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));

        line.Controls.Add(new Label
        {
            Text = "Niveles",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var levels = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10,
            Value = 5,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 0, 5)
        };
        line.Controls.Add(levels, 1, 0);
        AddRow(line, 40);
        return levels;
    }
}

public sealed class AnalyzerPanel : ModulePanelBase
{
    private readonly TextBox template = new();
    private readonly TextBox root = new();
    private readonly TextBox output = new();
    private readonly TextBox log = new();
    private readonly NumericUpDown levels;
    private readonly Button run = new();

    public AnalyzerPanel() : base("Analizador de combinaciones ACL")
    {
        AddPicker("Plantilla", template, (_, _) => template.Text = Ui.PickFile());
        AddPicker("Raíz NTFS", root, (_, _) => root.Text = Ui.PickFolder());
        AddPicker("Reporte", output, (_, _) =>
            output.Text = Ui.PickSave("Analisis_Combinaciones_ACL.xlsx"));

        levels = AddLevels();

        run.Text = "Analizar combinaciones";
        run.Width = 270;
        run.Height = 36;
        run.Anchor = AnchorStyles.Left;
        run.Margin = new Padding(115, 6, 0, 8);
        run.Click += Run;
        AddRow(run, 54);

        log.Multiline = true;
        log.ScrollBars = ScrollBars.Both;
        log.ReadOnly = true;
        log.MinimumSize = new Size(600, 130);
        AddRow(log, 145);
    }

    private async void Run(object? sender, EventArgs e)
    {
        if (!File.Exists(template.Text) ||
            !Directory.Exists(root.Text) ||
            string.IsNullOrWhiteSpace(output.Text))
        {
            MessageBox.Show("Complete plantilla, raíz y reporte.");
            return;
        }

        run.Enabled = false;
        try
        {
            await Task.Run(() => AclPatternAnalyzer.Run(
                template.Text,
                root.Text,
                (int)levels.Value,
                output.Text,
                (percent, message) => BeginInvoke(() =>
                    log.Text = percent + "%  " + message)));

            log.Text = "Reporte generado: " + output.Text;
            MessageBox.Show("Análisis terminado.");
        }
        catch (Exception ex)
        {
            log.Text = ex.ToString();
            MessageBox.Show(ex.Message, "Error");
        }
        finally
        {
            run.Enabled = true;
        }
    }
}

public sealed class ComparatorPanel : ModulePanelBase
{
    private readonly TextBox client = new();
    private readonly TextBox scan = new();
    private readonly TextBox output = new();
    private readonly NumericUpDown levels;

    public ComparatorPanel() : base("Comparador de matrices")
    {
        AddPicker("Matriz cliente", client, (_, _) => client.Text = Ui.PickFile());
        AddPicker("Matriz escaneada", scan, (_, _) => scan.Text = Ui.PickFile());
        AddPicker("Reporte", output, (_, _) =>
            output.Text = Ui.PickSave("Comparacion_Matrices.xlsx"));
        levels = AddLevels();

        var button = new Button
        {
            Text = "Comparar",
            Width = 220,
            Height = 36,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(115, 6, 0, 8)
        };
        button.Click += (_, _) =>
        {
            try
            {
                MatrixComparator.Run(
                    client.Text, scan.Text, output.Text, (int)levels.Value);
                MessageBox.Show("Comparación generada.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        };
        AddRow(button, 54);
    }
}

public sealed class PlannerPanel : ModulePanelBase
{
    private readonly TextBox matrix = new();
    private readonly TextBox root = new();
    private readonly TextBox output = new();
    private readonly NumericUpDown levels;

    public PlannerPanel() : base("Planeador de cambios amarillos")
    {
        AddPicker("Matriz cliente", matrix, (_, _) => matrix.Text = Ui.PickFile());
        AddPicker("Raíz NTFS", root, (_, _) => root.Text = Ui.PickFolder());
        AddPicker("Plan", output, (_, _) =>
            output.Text = Ui.PickSave("Plan_Cambios_NTFS.xlsx"));
        levels = AddLevels();

        var button = new Button
        {
            Text = "Generar plan (sin aplicar)",
            Width = 260,
            Height = 36,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(115, 6, 0, 8)
        };
        button.Click += (_, _) =>
        {
            try
            {
                ChangePlanner.Run(
                    matrix.Text, root.Text, output.Text, (int)levels.Value);
                MessageBox.Show("Plan generado. No se modificó NTFS.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        };
        AddRow(button, 54);
    }
}

public sealed class PlaceholderPanel : UserControl
{
    public PlaceholderPanel(string title, string text)
    {
        Dock = DockStyle.Fill;
        Controls.Add(new Label
        {
            Text = title,
            Left = 30,
            Top = 35,
            AutoSize = true,
            Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
        Controls.Add(new Label
        {
            Text = text,
            Left = 30,
            Top = 90,
            Width = 800,
            Height = 180,
            Font = new Font("Segoe UI", 11)
        });
    }
}
