using System;
using System.Drawing;
using System.Windows.Forms;

namespace HotelPOS.UI;

/// <summary>
/// ระบบ ToolTip ขนาดใหญ่ อ่านง่าย สบายตา พร้อมระบบป้องกันหน้าจอกินเฟรม/ตกขอบ 100%
/// </summary>
public class AppToolTip : ToolTip
{
    private static readonly Lazy<AppToolTip> _sharedInstance = new(() => new AppToolTip());
    public static AppToolTip Instance => _sharedInstance.Value;

    private readonly Font _titleFont = new("Segoe UI", 11.5F, FontStyle.Bold);
    private readonly Font _bodyFont = new("Segoe UI", 11F, FontStyle.Regular);

    public AppToolTip()
    {
        OwnerDraw = true;
        AutomaticDelay = 200;
        AutoPopDelay = 15000; // 15 seconds visibility
        InitialDelay = 200;
        ReshowDelay = 100;
        UseAnimation = true;
        UseFading = true;

        Popup += AppToolTip_Popup;
        Draw += AppToolTip_Draw;
    }

    /// <summary>
    /// ผูก ToolTip ขนาดใหญ่อ่านง่ายเข้ากับ คอนโทรล ต่างๆ
    /// </summary>
    public static void Attach(Control control, string caption)
    {
        if (control == null || string.IsNullOrWhiteSpace(caption)) return;
        Instance.SetToolTip(control, caption);
    }

    private void AppToolTip_Popup(object? sender, PopupEventArgs e)
    {
        if (e.AssociatedControl == null) return;

        string? text = GetToolTip(e.AssociatedControl);
        if (string.IsNullOrWhiteSpace(text)) return;

        // Calculate size using large 11.5pt font
        Size textSize = TextRenderer.MeasureText(text, _bodyFont, new Size(420, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

        // Add 24px padding width, 16px padding height
        int width = Math.Max(160, textSize.Width + 28);
        int height = Math.Max(42, textSize.Height + 18);

        // Safety Position Check against Screen Frame Boundaries
        Point mousePos = Cursor.Position;
        Screen screen = Screen.FromPoint(mousePos);
        Rectangle workingArea = screen.WorkingArea;

        // If tooltip extends past right border of screen, auto shift left
        if (mousePos.X + width + 15 > workingArea.Right)
        {
            // Position safely inside working area
            int shiftX = (mousePos.X + width + 15) - workingArea.Right;
            mousePos.X -= shiftX;
        }

        // If tooltip extends past bottom border of screen, auto shift up
        if (mousePos.Y + height + 24 > workingArea.Bottom)
        {
            mousePos.Y -= (height + 30);
        }

        e.ToolTipSize = new Size(width, height);
    }

    private void AppToolTip_Draw(object? sender, DrawToolTipEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle bounds = e.Bounds;

        // 1. Background Fill (Sleek Dark Slate / Midnight Navy)
        using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillRectangle(bgBrush, bounds);

        // 2. Accent Top Line (Sky Blue Highlight)
        using var accentBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
        g.FillRectangle(accentBrush, new Rectangle(bounds.Left, bounds.Top, bounds.Width, 3));

        // 3. Crisp Border
        using var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.5f);
        g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        // 4. Large Readable Text
        Rectangle textRect = new Rectangle(bounds.Left + 14, bounds.Top + 10, bounds.Width - 24, bounds.Height - 14);
        TextRenderer.DrawText(
            g,
            e.ToolTipText,
            _bodyFont,
            textRect,
            Color.FromArgb(241, 245, 249), // Pure Soft White
            TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
