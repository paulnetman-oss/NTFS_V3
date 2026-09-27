namespace NTFSSuite;

public sealed class MainForm : Form
{
    private readonly Panel host = new();

    public MainForm()
    {
        Text = "NTFS Suite v3.2";
        Width = 1160;
        Height = 700;
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        var menu = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = Color.FromArgb(31, 78, 121)
        };

        host.Dock = DockStyle.Fill;
        Controls.Add(host);
        Controls.Add(menu);

        menu.Controls.Add(new Label
        {
            Text = "NTFS SUITE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            Left = 25,
            Top = 25,
            AutoSize = true
        });

        int y = 90;
        AddButton(menu, "Generar matriz", ref y, () => ShowModule(
            new PlaceholderPanel(
                "Generador de matriz",
                "Módulo reservado hasta aprobar el catálogo de combinaciones ACL.")));

        AddButton(menu, "Analizar ACL", ref y,
            () => ShowModule(new AnalyzerPanel()));

        AddButton(menu, "Analizar SID", ref y,
            () => ShowModule(new SidAnalyzerPanel()));

        AddButton(menu, "Comparar matrices", ref y,
            () => ShowModule(new ComparatorPanel()));

        AddButton(menu, "Planear cambios", ref y,
            () => ShowModule(new PlannerPanel()));

        AddButton(menu, "Aplicar permisos", ref y, () => ShowModule(
            new PlaceholderPanel(
                "Aplicador de permisos",
                "Módulo bloqueado. Primero deben aprobarse el catálogo ACL, respaldo, verificación y reversión.")));

        ShowModule(new AnalyzerPanel());
    }

    private static void AddButton(
        Panel panel,
        string text,
        ref int y,
        Action action)
    {
        var button = new Button
        {
            Text = text,
            Left = 15,
            Top = y,
            Width = 190,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 95, 135),
            ForeColor = Color.White
        };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
        y += 52;
    }

    private void ShowModule(Control control)
    {
        host.Controls.Clear();
        host.Controls.Add(control);
    }
}
