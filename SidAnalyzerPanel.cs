namespace NTFSSuite;

public sealed class SidAnalyzerPanel : UserControl
{
    private readonly TextBox template = new();
    private readonly TextBox root = new();
    private readonly TextBox output = new();
    private readonly TextBox log = new();
    private readonly NumericUpDown levels = new();
    private readonly ProgressBar progress = new();
    private readonly Button run = new();

    public SidAnalyzerPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;

        int y = 20;
        Controls.Add(new Label
        {
            Text = "Analizador de origen de SID",
            Left = 20,
            Top = y,
            AutoSize = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold)
        });
        y += 52;

        AddPickerLine("Plantilla", template, ref y,
            () => template.Text = Ui.PickFile());

        AddPickerLine("Raíz NTFS", root, ref y,
            () => root.Text = Ui.PickFolder());

        AddPickerLine("Libro de salida", output, ref y,
            () => output.Text = Ui.PickSave("Analisis_Origen_SID.xlsx"));

        Controls.Add(new Label
        {
            Text = "Niveles",
            Left = 20,
            Top = y + 5,
            Width = 115
        });
        levels.SetBounds(140, y, 80, 28);
        levels.Minimum = 1;
        levels.Maximum = 10;
        levels.Value = 5;
        Controls.Add(levels);
        y += 45;

        run.Text = "Analizar origen de SID";
        run.SetBounds(140, y, 270, 38);
        run.Click += Run;
        Controls.Add(run);
        y += 52;

        progress.SetBounds(20, y, 790, 22);
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(progress);
        y += 32;

        log.SetBounds(20, y, 790, 140);
        log.Multiline = true;
        log.ScrollBars = ScrollBars.Both;
        log.ReadOnly = true;
        log.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                     AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(log);
    }

    private void AddPickerLine(
        string label,
        TextBox textBox,
        ref int y,
        Action action)
    {
        Controls.Add(new Label
        {
            Text = label,
            Left = 20,
            Top = y + 5,
            Width = 115
        });

        textBox.SetBounds(140, y, 535, 28);
        textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(textBox);

        var button = new Button
        {
            Text = "Examinar",
            Left = 685,
            Top = y - 1,
            Width = 125,
            Height = 31,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        button.Click += (_, _) => action();
        Controls.Add(button);

        // Mantiene visible el botón al cambiar el tamaño o el escalado.
        void ResizeLine(object? sender, EventArgs args)
        {
            int rightMargin = 25;
            button.Left = Math.Max(520, ClientSize.Width - button.Width - rightMargin);
            textBox.Width = Math.Max(250, button.Left - textBox.Left - 10);
        }

        Resize += ResizeLine;
        ResizeLine(this, EventArgs.Empty);
        y += 43;
    }

    private async void Run(object? sender, EventArgs e)
    {
        if (!File.Exists(template.Text) ||
            !Directory.Exists(root.Text) ||
            string.IsNullOrWhiteSpace(output.Text))
        {
            MessageBox.Show(
                "Seleccione plantilla, raíz NTFS y libro de salida.",
                "NTFS Suite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        run.Enabled = false;
        progress.Value = 0;
        log.Clear();

        try
        {
            await Task.Run(() => SidOriginAnalyzer.Run(
                template.Text,
                root.Text,
                (int)levels.Value,
                output.Text,
                (percent, message) =>
                {
                    BeginInvoke(() =>
                    {
                        progress.Value = Math.Max(0, Math.Min(100, percent));
                        log.Text = percent + "%  " + message;
                    });
                }));

            log.Text = "Libro generado: " + output.Text;
            MessageBox.Show(
                "Análisis de SID terminado. No se modificó ninguna ACL.",
                "NTFS Suite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            log.Text = ex.ToString();
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            run.Enabled = true;
        }
    }
}
