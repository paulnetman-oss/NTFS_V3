namespace NTFSSuite;

public static class Ui
{
    public static Control AddPickerLine(
        Control parent,
        string labelText,
        TextBox textBox,
        int row,
        EventHandler click)
    {
        var line = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Padding(0),
            Padding = new Padding(0),
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = false
        };

        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
        line.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0)
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 5, 10, 5);

        var button = new Button
        {
            Text = "Examinar",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            MinimumSize = new Size(115, 30),
            AutoSize = false
        };
        button.Click += click;

        line.Controls.Add(label, 0, 0);
        line.Controls.Add(textBox, 1, 0);
        line.Controls.Add(button, 2, 0);
        parent.Controls.Add(line);
        line.BringToFront();
        return line;
    }

    // Se conserva para compatibilidad con paneles anteriores.
    public static void Line(
        Control parent,
        string label,
        TextBox box,
        ref int y,
        EventHandler click,
        bool folder = false)
    {
        var line = new TableLayoutPanel
        {
            Left = 20,
            Top = y,
            Width = Math.Max(500, parent.ClientSize.Width - 40),
            Height = 38,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));

        var caption = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0)
        };

        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 4, 10, 4);

        var button = new Button
        {
            Text = "Examinar",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2),
            MinimumSize = new Size(115, 30)
        };
        button.Click += click;

        line.Controls.Add(caption, 0, 0);
        line.Controls.Add(box, 1, 0);
        line.Controls.Add(button, 2, 0);
        parent.Controls.Add(line);
        line.BringToFront();
        y += 42;
    }

    public static string PickFile(string filter = "Excel (*.xlsx)|*.xlsx")
    {
        using var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : "";
    }

    public static string PickSave(string name)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = name,
            DefaultExt = "xlsx"
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : "";
    }

    public static string PickFolder()
    {
        using var dialog = new FolderBrowserDialog();
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : "";
    }
}
