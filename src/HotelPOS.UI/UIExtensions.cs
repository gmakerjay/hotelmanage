using System.Reflection;
using System.Windows.Forms;

namespace HotelPOS.UI;

/// <summary>
/// ส่วนขยายสำหรับการเพิ่มประสิทธิภาพ UI และลบอาการกะพริบ (Flicker-free DoubleBuffering)
/// </summary>
public static class UIExtensions
{
    /// <summary>
    /// เปิดใช้งาน DoubleBuffering ให้กับ DataGridView เพื่อขจัดอาการกะพริบ และเร่งความเร็วในการ Render 60 FPS
    /// </summary>
    public static void EnableDoubleBuffering(this DataGridView dgv)
    {
        if (dgv == null) return;
        try
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                dgv,
                new object[] { true });
        }
        catch { }
    }

    /// <summary>
    /// เปิดใช้งาน DoubleBuffering ให้กับ Control เช่น Panel, FlowLayoutPanel
    /// </summary>
    public static void EnableDoubleBuffering(this Control control)
    {
        if (control == null) return;
        try
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                control,
                new object[] { true });
        }
        catch { }
    }

    /// <summary>
    /// กำหนดรูปแบบสลับสีบรรทัด (Zebra Striping: ขาว สลับ ฟ้าเทาอ่อน) ให้กับตารางข้อมูล DataGridView
    /// </summary>
    public static void ApplyZebraStyle(this DataGridView dgv)
    {
        if (dgv == null) return;
        dgv.EnableDoubleBuffering();
        dgv.RowHeadersVisible = false; // ปิดช่องหน้าสุดซ้ายมือตามคำสั่งผู้ใช้
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(243, 246, 251); // ริ้วบรรทัดสลับ
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(14, 116, 144);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
    }
}
