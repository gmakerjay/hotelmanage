using System;
using System.Drawing;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.UI;

public enum RoomUserAction
{
    None,
    CheckInWalkIn,
    Reserve,
    CheckOut,
    RecordMeter,
    POS,
    CheckInReserved,
    CancelReserved,
    CleaningDone,
    MaintenanceStart,
    MaintenanceDone,
    AdminOverrideStatus,
    PayUtilityNow
}

/// <summary>
/// หน้าต่างป๊อปอัพจัดการห้องพัก (Pop-up Modal) เมื่อผู้ใช้คลิกเลือกห้องพักจากผังหลัก
/// แสดงรายละเอียดห้อง ผู้เข้าพัก สถานะ และปุ่มเลือกทำรายการอย่างชัดเจน ขจัดความสับสน
/// </summary>
public class RoomActionForm : Form
{
    public RoomUserAction SelectedAction { get; private set; } = RoomUserAction.None;

    public RoomActionForm(
        Room room,
        RoomType? roomType,
        Booking? activeBooking,
        Customer? activeCustomer,
        bool isUtilityOverdue = false,
        bool isUtilityDueSoon = false,
        decimal totalUnpaid = 0)
    {
        InitializeComponent(room, roomType, activeBooking, activeCustomer, isUtilityOverdue, isUtilityDueSoon, totalUnpaid);
    }

    private void InitializeComponent(
        Room room,
        RoomType? roomType,
        Booking? booking,
        Customer? customer,
        bool isUtilityOverdue,
        bool isUtilityDueSoon,
        decimal totalUnpaid)
    {
        Text = $"จัดการห้องพัก {room.RoomNumber} - HotelPOS";
        Size = new Size(530, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        // Header Panel (แถบส่วนหัวสีเด่นชัดตามประเภทห้องและสถานะ)
        var colorHex = roomType?.ColorHex ?? "#0284C7";
        Color typeColor;
        try { typeColor = ColorTranslator.FromHtml(colorHex); } catch { typeColor = Color.FromArgb(2, 132, 199); }

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblRoomNum = new Label
        {
            Text = $"ห้อง {room.RoomNumber}",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(16, 12),
            AutoSize = true
        };

        var typeBadge = new Label
        {
            Text = $" {roomType?.Name ?? "Standard"} ",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = typeColor,
            ForeColor = Color.White,
            Location = new Point(18, 48),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2)
        };

        var floorBadge = new Label
        {
            Text = string.IsNullOrWhiteSpace(room.Floor) ? "" : room.Floor,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(typeBadge.Right + 12, 50),
            AutoSize = true
        };

        // Status Badge at Header Right (ไม่มีอิโมจิ)
        string statusText;
        Color statusBg;
        switch (room.Status)
        {
            case RoomStatus.Available:
                statusText = "ห้องว่าง พร้อมเข้าพัก";
                statusBg = Color.FromArgb(16, 185, 129);
                break;
            case RoomStatus.Occupied:
                statusText = isUtilityOverdue ? "มีผู้พัก (เลยกำหนดค่าน้ำไฟ!)" : "มีผู้เข้าพัก";
                statusBg = isUtilityOverdue ? Color.FromArgb(185, 28, 28) : Color.FromArgb(37, 99, 235);
                break;
            case RoomStatus.Reserved:
                statusText = "จองล่วงหน้า";
                statusBg = Color.FromArgb(139, 92, 246);
                break;
            case RoomStatus.Cleaning:
                statusText = "รอทำความสะอาด";
                statusBg = Color.FromArgb(245, 158, 11);
                break;
            case RoomStatus.Maintenance:
            default:
                statusText = "ปิดซ่อมบำรุง";
                statusBg = Color.FromArgb(100, 116, 139);
                break;
        }

        var statusPill = new Label
        {
            Text = statusText,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = statusBg,
            ForeColor = Color.White,
            Location = new Point(290, 24),
            Size = new Size(205, 36),
            TextAlign = ContentAlignment.MiddleCenter
        };

        headerPanel.Controls.Add(lblRoomNum);
        headerPanel.Controls.Add(typeBadge);
        headerPanel.Controls.Add(floorBadge);
        headerPanel.Controls.Add(statusPill);

        // Details Panel (กล่องสรุปรายละเอียดห้อง/ผู้พัก)
        var detailsGroup = new GroupBox
        {
            Text = "ข้อมูลห้องพัก และผู้เข้าพัก",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(16, 95),
            Size = new Size(482, 175),
            BackColor = Color.White
        };

        var detailsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.TopDown,
            AutoScroll = false,
            WrapContents = false
        };

        // Price rates
        string rateStr = "";
        if (roomType != null)
        {
            if (roomType.DailyRate > 0) rateStr += $"รายวัน {roomType.DailyRate:N0} บ. ";
            if (roomType.HourlyRate > 0) rateStr += $"| รายชั่วโมง {roomType.HourlyRate:N0} บ./ชม. ";
            if (roomType.MonthlyRate > 0) rateStr += $"| รายเดือน {roomType.MonthlyRate:N0} บ.";
        }
        var lblRates = new Label
        {
            Text = $"อัตราค่าเช่า: {rateStr}",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        detailsLayout.Controls.Add(lblRates);

        if (customer != null)
        {
            string planText = booking?.RatePlan == RatePlanType.Daily ? "รายวัน" : (booking?.RatePlan == RatePlanType.Hourly ? "รายชั่วโมง" : "รายเดือน");
            var lblGuestInfo = new Label
            {
                Text = $"ผู้พัก/ผู้จอง: {customer.FullName} (เบอร์โทร: {customer.Phone ?? "-"}) [{planText}]",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 58, 138),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            detailsLayout.Controls.Add(lblGuestInfo);

            if (booking?.CheckInPlanned != null)
            {
                var lblCheckIn = new Label
                {
                    Text = $"วันที่เช็คอิน: {booking.CheckInPlanned:dd/MM/yyyy HH:mm} น.",
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 2)
                };
                detailsLayout.Controls.Add(lblCheckIn);
            }

            if (booking?.CheckOutPlanned != null)
            {
                var lblCheckOut = new Label
                {
                    Text = $"กำหนดเช็คเอาท์: {booking.CheckOutPlanned:dd/MM/yyyy HH:mm} น.",
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 2)
                };
                detailsLayout.Controls.Add(lblCheckOut);
            }
        }

        if (isUtilityOverdue || totalUnpaid > 0)
        {
            var pnlMeterAlertCard = new Panel
            {
                Size = new Size(450, 36),
                BackColor = Color.FromArgb(254, 242, 242),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 0, 0),
                Padding = new Padding(8, 4, 8, 4)
            };

            var lblMeterAlert = new Label
            {
                Text = $"มียอดค่าน้ำ-ค่าไฟ ค้างชำระ: {totalUnpaid:N2} บาท",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(185, 28, 28),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            pnlMeterAlertCard.Controls.Add(lblMeterAlert);
            detailsLayout.Controls.Add(pnlMeterAlertCard);
        }

        detailsGroup.Controls.Add(detailsLayout);

        // Action Buttons Panel (ปุ่มกดเลือกทำรายการหลัก - กะทัดรัด คลีน ไม่มีอิโมจิ)
        var actionGroup = new GroupBox
        {
            Text = "เลือกทำรายการที่ต้องการ",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(16, 280),
            Size = new Size(482, 215),
            BackColor = Color.White
        };

        var actionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false
        };

        var btnMeter = CreateActionButton("จดมิเตอร์น้ำ-ไฟ / ออกบิลประจำเดือน", Color.FromArgb(14, 116, 144), Color.White);
        btnMeter.Click += (s, e) => { SelectedAction = RoomUserAction.RecordMeter; DialogResult = DialogResult.OK; };

        if (room.Status == RoomStatus.Available)
        {
            var btnWalkIn = CreateActionButton("เช็คอินทันที (Walk-In)", Color.FromArgb(16, 185, 129), Color.White);
            btnWalkIn.Click += (s, e) => { SelectedAction = RoomUserAction.CheckInWalkIn; DialogResult = DialogResult.OK; };

            var btnReserve = CreateActionButton("จองห้องพักล่วงหน้า", Color.FromArgb(37, 99, 235), Color.White);
            btnReserve.Click += (s, e) => { SelectedAction = RoomUserAction.Reserve; DialogResult = DialogResult.OK; };

            var btnMaint = CreateActionButton("ปิดซ่อมบำรุงห้องพัก", Color.FromArgb(100, 116, 139), Color.White);
            btnMaint.Click += (s, e) => { SelectedAction = RoomUserAction.MaintenanceStart; DialogResult = DialogResult.OK; };

            actionsFlow.Controls.AddRange(new Control[] { btnWalkIn, btnReserve, btnMaint });
        }
        else if (room.Status == RoomStatus.Occupied)
        {
            var btnCheckOut = CreateActionButton("คืนห้องพัก / เช็คเอาท์ & ออกบิล", Color.FromArgb(225, 29, 72), Color.White);
            btnCheckOut.Click += (s, e) => { SelectedAction = RoomUserAction.CheckOut; DialogResult = DialogResult.OK; };

            var btnPOS = CreateActionButton("สั่งมินิบาร์ / สั่งสินค้า (POS)", Color.FromArgb(124, 58, 237), Color.White);
            btnPOS.Click += (s, e) => { SelectedAction = RoomUserAction.POS; DialogResult = DialogResult.OK; };

            actionsFlow.Controls.AddRange(new Control[] { btnCheckOut, btnMeter, btnPOS });
        }
        else if (room.Status == RoomStatus.Reserved)
        {
            var btnCheckInReserved = CreateActionButton("เช็คอิน (จากการจอง)", Color.FromArgb(16, 185, 129), Color.White);
            btnCheckInReserved.Click += (s, e) => { SelectedAction = RoomUserAction.CheckInReserved; DialogResult = DialogResult.OK; };

            var btnCancelReserve = CreateActionButton("ยกเลิกการจองห้องพักนี้", Color.FromArgb(220, 38, 38), Color.White);
            btnCancelReserve.Click += (s, e) => { SelectedAction = RoomUserAction.CancelReserved; DialogResult = DialogResult.OK; };

            actionsFlow.Controls.AddRange(new Control[] { btnCheckInReserved, btnCancelReserve });
        }
        else if (room.Status == RoomStatus.Cleaning)
        {
            var btnCleanDone = CreateActionButton("ทำความสะอาดเสร็จแล้ว (เปลี่ยนเป็นห้องว่าง)", Color.FromArgb(16, 185, 129), Color.White);
            btnCleanDone.Click += (s, e) => { SelectedAction = RoomUserAction.CleaningDone; DialogResult = DialogResult.OK; };

            actionsFlow.Controls.Add(btnCleanDone);
        }
        else if (room.Status == RoomStatus.Maintenance)
        {
            var btnMaintDone = CreateActionButton("ซ่อมเสร็จแล้ว (เปลี่ยนเป็นห้องว่างพร้อมใช้งาน)", Color.FromArgb(16, 185, 129), Color.White);
            btnMaintDone.Click += (s, e) => { SelectedAction = RoomUserAction.MaintenanceDone; DialogResult = DialogResult.OK; };

            actionsFlow.Controls.Add(btnMaintDone);
        }

        actionGroup.Controls.Add(actionsFlow);

        var btnClose = new Button
        {
            Text = "ปิดหน้าต่าง",
            Size = new Size(130, 40),
            Location = new Point(368, 515),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => DialogResult = DialogResult.Cancel;

        Controls.Add(headerPanel);
        Controls.Add(detailsGroup);
        Controls.Add(actionGroup);
        Controls.Add(btnClose);
    }

    private Button CreateActionButton(string text, Color backColor, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(444, 46),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleCenter
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
