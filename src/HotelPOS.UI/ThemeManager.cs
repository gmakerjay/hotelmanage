using System.Drawing;

namespace HotelPOS.UI;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Slate";
    public static string CurrentFontSize { get; private set; } = "Medium";

    public static event Action? OnThemeChanged;

    public static Color PrimaryColor { get; private set; } = Color.FromArgb(37, 99, 235);
    public static Color SidebarColor { get; private set; } = Color.FromArgb(20, 20, 30);
    public static Color BackgroundColor { get; private set; } = Color.FromArgb(241, 245, 249);
    public static Color CardColor { get; private set; } = Color.White;
    public static Color TextColor { get; private set; } = Color.FromArgb(30, 41, 59);
    public static Color BorderColor { get; private set; } = Color.FromArgb(226, 232, 240);

    public static float BaseFontSize { get; private set; } = 10.5F;

    public static void ApplyTheme(string themeName, string fontSizeName)
    {
        CurrentTheme = string.IsNullOrWhiteSpace(themeName) ? "Slate" : themeName;
        CurrentFontSize = string.IsNullOrWhiteSpace(fontSizeName) ? "Medium" : fontSizeName;

        switch (CurrentTheme)
        {
            case "Emerald":
                SidebarColor = Color.FromArgb(6, 78, 59);        // Deep Emerald
                PrimaryColor = Color.FromArgb(16, 185, 129);     // Emerald Green
                BackgroundColor = Color.FromArgb(240, 253, 244);  // Light Tint
                CardColor = Color.White;
                TextColor = Color.FromArgb(6, 78, 59);
                BorderColor = Color.FromArgb(209, 250, 229);
                break;

            case "Dark":
                SidebarColor = Color.FromArgb(15, 23, 42);       // Midnight Dark
                PrimaryColor = Color.FromArgb(59, 130, 246);     // Neon Royal Blue
                BackgroundColor = Color.FromArgb(30, 41, 59);    // Dark Slate
                CardColor = Color.FromArgb(51, 65, 85);         // Slate Card
                TextColor = Color.FromArgb(248, 250, 252);      // Crisp Light Text
                BorderColor = Color.FromArgb(71, 85, 105);
                break;

            case "Sapphire":
                SidebarColor = Color.FromArgb(30, 27, 75);       // Royal Indigo
                PrimaryColor = Color.FromArgb(79, 70, 229);      // Sapphire Indigo
                BackgroundColor = Color.FromArgb(238, 242, 255); // Indigo Tint
                CardColor = Color.White;
                TextColor = Color.FromArgb(30, 27, 75);
                BorderColor = Color.FromArgb(224, 231, 255);
                break;

            case "Amber":
                SidebarColor = Color.FromArgb(69, 26, 3);        // Deep Wood Amber
                PrimaryColor = Color.FromArgb(217, 119, 6);      // Golden Amber
                BackgroundColor = Color.FromArgb(254, 243, 199); // Warm Light Amber
                CardColor = Color.White;
                TextColor = Color.FromArgb(69, 26, 3);
                BorderColor = Color.FromArgb(253, 230, 138);
                break;

            case "Slate":
            default:
                SidebarColor = Color.FromArgb(20, 20, 30);       // Modern Slate Dark
                PrimaryColor = Color.FromArgb(37, 99, 235);      // Electric Blue
                BackgroundColor = Color.FromArgb(241, 245, 249); // Light Slate
                CardColor = Color.White;
                TextColor = Color.FromArgb(30, 41, 59);
                BorderColor = Color.FromArgb(226, 232, 240);
                break;
        }

        switch (CurrentFontSize)
        {
            case "Standard":
                BaseFontSize = 10F;
                break;
            case "Large":
                BaseFontSize = 11F;
                break;
            case "ExtraLarge":
                BaseFontSize = 11.5F;
                break;
            case "Medium":
            default:
                BaseFontSize = 10.5F;
                break;
        }

        OnThemeChanged?.Invoke();
    }

    public static void ApplyThemeToControlTree(Control root)
    {
        if (root == null) return;

        try
        {
            root.SuspendLayout();

            if (root is Form form)
            {
                form.BackColor = (CurrentTheme == "Dark") ? Color.FromArgb(15, 23, 42) : BackgroundColor;
            }
            else if (root is UserControl uc && uc.BackColor != Color.Transparent)
            {
                uc.BackColor = BackgroundColor;
            }

            ApplyThemeRecursive(root);
        }
        catch { }
        finally
        {
            root.ResumeLayout(true);
        }
    }

    private static void ApplyThemeRecursive(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            // Apply theme colors without breaking pixel layout positions
            if (c is Label lbl)
            {
                if (lbl.ForeColor != Color.ForestGreen &&
                    lbl.ForeColor != Color.DarkGreen &&
                    lbl.ForeColor != Color.FromArgb(220, 38, 38) &&
                    lbl.ForeColor != Color.FromArgb(239, 68, 68) &&
                    lbl.ForeColor != Color.White &&
                    lbl.ForeColor != Color.FromArgb(217, 119, 6))
                {
                    lbl.ForeColor = TextColor;
                }
            }
            else if (c is Button btn)
            {
                // Primary action buttons (Blue/Green/Accent)
                if (btn.BackColor == Color.FromArgb(37, 99, 235) ||
                    btn.BackColor == Color.FromArgb(16, 185, 129) ||
                    btn.BackColor == Color.ForestGreen ||
                    btn.BackColor == Color.FromArgb(79, 70, 229) ||
                    btn.BackColor == Color.FromArgb(30, 27, 75))
                {
                    btn.BackColor = PrimaryColor;
                    btn.ForeColor = Color.White;
                }
                else if (btn.BackColor == Color.White || btn.BackColor == Color.FromArgb(241, 245, 249))
                {
                    btn.BackColor = CardColor;
                    btn.ForeColor = TextColor;
                }
            }
            else if (c is TextBox txt)
            {
                txt.BackColor = CardColor;
                txt.ForeColor = TextColor;
            }
            else if (c is ComboBox cbo)
            {
                cbo.BackColor = CardColor;
                cbo.ForeColor = TextColor;
            }
            else if (c is DataGridView dgv)
            {
                dgv.BackgroundColor = CardColor;
                dgv.DefaultCellStyle.BackColor = CardColor;
                dgv.DefaultCellStyle.ForeColor = TextColor;
                dgv.DefaultCellStyle.SelectionBackColor = PrimaryColor;
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = SidebarColor;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgv.EnableHeadersVisualStyles = false;
            }
            else if (c is Panel pnl)
            {
                if (pnl.BackColor == Color.White || pnl.BackColor == Color.FromArgb(248, 250, 252))
                {
                    pnl.BackColor = CardColor;
                }
                else if (pnl.BackColor == Color.FromArgb(241, 245, 249))
                {
                    pnl.BackColor = BackgroundColor;
                }
            }
            else if (c is GroupBox grp)
            {
                grp.ForeColor = TextColor;
                if (grp.BackColor == Color.White || grp.BackColor == Color.FromArgb(241, 245, 249))
                {
                    grp.BackColor = CardColor;
                }
            }

            // Recurse into child controls
            if (c.HasChildren)
            {
                ApplyThemeRecursive(c);
            }
        }
    }
}
