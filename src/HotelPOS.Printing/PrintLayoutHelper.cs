using System;
using System.Drawing;
using System.IO;

namespace HotelPOS.Printing;

/// <summary>
/// ตัวช่วยส่วนกลางสำหรับการจัดหน้า วาดข้อความ และจัดการทรัพยากรภาพในโมดูลการพิมพ์
/// </summary>
public static class PrintLayoutHelper
{
    /// <summary>
    /// โหลดไฟล์ภาพจากพาธดิสก์อย่างปลอดภัยโดยไม่ล็อกไฟล์บนระบบปฏิบัติการ (File-Lock Safe)
    /// </summary>
    public static Image? LoadImageSafe(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            using var ms = new MemoryStream(bytes);
            using var tempImg = Image.FromStream(ms);
            return new Bitmap(tempImg);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// วาดข้อความจัดแนวซ้าย-ขวาบนกระดาษสลิปความร้อน (Thermal Receipt) พร้อมคำนวณความสูงแถว
    /// </summary>
    public static float DrawLeftRight(
        Graphics g,
        string left,
        string right,
        Font fLeft,
        Font fRight,
        float drawY,
        float leftMargin,
        float rightMargin,
        float contentWidth,
        float scale = 1.0f)
    {
        var sizeRight = g.MeasureString(right, fRight);
        float maxLeftWidth = Math.Max(30f, contentWidth - sizeRight.Width - 6f);
        var sizeLeft = g.MeasureString(left, fLeft, (int)maxLeftWidth);
        float rowHeight = Math.Max(sizeLeft.Height, sizeRight.Height);

        g.DrawString(left, fLeft, Brushes.Black, new RectangleF(leftMargin, drawY, maxLeftWidth, rowHeight));
        g.DrawString(right, fRight, Brushes.Black, new RectangleF(rightMargin - sizeRight.Width, drawY, sizeRight.Width + 4f, rowHeight));

        return Math.Max(18f * scale, rowHeight);
    }
}
