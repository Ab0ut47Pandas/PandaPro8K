using System.Runtime.InteropServices;

namespace PandaPro8K;

internal sealed class DarkGroupBox : GroupBox
{
    private static readonly Color DarkBorder = Color.FromArgb(68, 78, 90);

    public DarkGroupBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int borderTop = Math.Max(8, Font.Height / 2);
        var border = new Rectangle(0, borderTop, Math.Max(0, Width - 1), Math.Max(0, Height - borderTop - 1));
        using var pen = new Pen(DarkBorder);
        e.Graphics.DrawRectangle(pen, border);

        if (string.IsNullOrEmpty(Text)) return;
        Size titleSize = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var titleBackground = new Rectangle(8, 0, titleSize.Width + 8, Font.Height + 2);
        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, titleBackground);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Point(12, 0),
            ForeColor,
            TextFormatFlags.NoPadding);
    }
}

internal sealed class DarkProgressBar : ProgressBar
{
    private static readonly Color DarkTrack = Color.FromArgb(45, 52, 61);
    private static readonly Color DarkFill = Color.FromArgb(0, 170, 110);
    private static readonly Color DarkBorder = Color.FromArgb(68, 78, 90);

    public DarkProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(DarkTrack);
        double range = Maximum - Minimum;
        double ratio = range <= 0 ? 0 : Math.Clamp((Value - Minimum) / range, 0, 1);
        int fillWidth = (int)Math.Round((Width - 2) * ratio);
        if (fillWidth > 0)
        {
            using var fill = new SolidBrush(DarkFill);
            e.Graphics.FillRectangle(fill, 1, 1, fillWidth, Math.Max(0, Height - 2));
        }
        using var border = new Pen(DarkBorder);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }
}

internal sealed class DarkTabControl : TabControl
{
    private static readonly Color SelectedTabBackground = Color.FromArgb(26, 31, 37);
    private static readonly Color IdleTabBackground = Color.FromArgb(15, 18, 23);
    private static readonly Color SelectedText = Color.FromArgb(232, 236, 240);
    private static readonly Color IdleText = Color.FromArgb(164, 174, 184);
    private static readonly Color DarkBorder = Color.FromArgb(68, 78, 90);

    public DarkTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Appearance = TabAppearance.FlatButtons;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(92, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        for (int index = 0; index < TabCount; index++)
        {
            Rectangle tab = GetTabRect(index);
            bool selected = index == SelectedIndex;
            using var background = new SolidBrush(selected ? SelectedTabBackground : IdleTabBackground);
            e.Graphics.FillRectangle(background, tab);
            TextRenderer.DrawText(
                e.Graphics,
                TabPages[index].Text,
                Font,
                tab,
                selected ? SelectedText : IdleText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (TabCount > 0)
        {
            int y = GetTabRect(0).Bottom;
            using var border = new Pen(DarkBorder);
            e.Graphics.DrawLine(border, 0, y, Width, y);
        }
    }
}

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    private static readonly Color MenuBackground = Color.FromArgb(13, 17, 21);
    private static readonly Color MenuSelection = Color.FromArgb(51, 59, 68);
    private static readonly Color MenuBorderColor = Color.FromArgb(68, 78, 90);

    public override Color ToolStripDropDownBackground => MenuBackground;
    public override Color ImageMarginGradientBegin => MenuBackground;
    public override Color ImageMarginGradientMiddle => MenuBackground;
    public override Color ImageMarginGradientEnd => MenuBackground;
    public override Color MenuItemSelected => MenuSelection;
    public override Color MenuItemSelectedGradientBegin => MenuSelection;
    public override Color MenuItemSelectedGradientEnd => MenuSelection;
    public override Color MenuItemBorder => MenuBorderColor;
    public override Color MenuBorder => MenuBorderColor;
    public override Color SeparatorDark => MenuBorderColor;
    public override Color SeparatorLight => MenuBorderColor;
}

internal static class DarkWindow
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeBefore20H1 = 19;

    public static void Apply(Form form)
    {
        void ApplyNow()
        {
            int enabled = 1;
            if (DwmSetWindowAttribute(form.Handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, UseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            ApplyThemes(form);
        }

        if (form.IsHandleCreated) ApplyNow();
        form.HandleCreated += (_, _) => ApplyNow();
        form.Shown += (_, _) => ApplyThemes(form);
    }

    private static void ApplyThemes(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.IsHandleCreated)
            {
                string theme = control is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer";
                SetWindowTheme(control.Handle, theme, null);
            }
            ApplyThemes(control);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);
}
