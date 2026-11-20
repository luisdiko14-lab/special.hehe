using System.Drawing;
using System.Windows.Forms;

namespace Nexus;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(13, 17, 27);
    public static readonly Color Sidebar = Color.FromArgb(17, 22, 34);
    public static readonly Color Surface = Color.FromArgb(24, 31, 46);
    public static readonly Color SurfaceRaised = Color.FromArgb(31, 40, 58);
    public static readonly Color Accent = Color.FromArgb(101, 130, 255);
    public static readonly Color AccentHover = Color.FromArgb(119, 145, 255);
    public static readonly Color AccentSoft = Color.FromArgb(35, 48, 83);
    public static readonly Color SurfaceHover = Color.FromArgb(37, 47, 68);
    public static readonly Color Text = Color.FromArgb(239, 243, 252);
    public static readonly Color Muted = Color.FromArgb(151, 164, 188);
    public static readonly Color Border = Color.FromArgb(43, 54, 75);
    public static readonly Color Good = Color.FromArgb(75, 204, 145);
    public static readonly Color Warning = Color.FromArgb(245, 184, 91);

    public static Label Heading(string value, float size = 19)
    {
        return new Label
        {
            Text = value,
            AutoSize = true,
            ForeColor = Text,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            BackColor = Color.Transparent
        };
    }

    public static Label MutedLabel(string value, float size = 9)
    {
        return new Label
        {
            Text = value,
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", size),
            BackColor = Color.Transparent
        };
    }

    public static Button Button(string value, bool primary, EventHandler handler)
    {
        var button = new Button
        {
            Text = value,
            AutoSize = false,
            Height = 40,
            Width = 148,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : SurfaceRaised,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 9, 8),
            TextAlign = ContentAlignment.MiddleCenter
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(83, 109, 226) : AccentSoft;
        button.Click += handler;
        return button;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.ColumnHeadersHeight = 42;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 66, 105);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9);
        grid.RowTemplate.Height = 38;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }
}