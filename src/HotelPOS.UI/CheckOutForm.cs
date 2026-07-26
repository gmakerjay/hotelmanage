using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class CheckOutForm : Form
{
    private readonly Room _room;
    private readonly Booking _booking;
    private readonly Customer? _customer;
    private readonly Folio? _folio;
    private readonly IBookingService _bookingService;

    private Label _lblCustomer = null!;
    private Label _lblCheckInTime = null!;
    private Label _lblDuration = null!;
    private NumericUpDown _numRoomCharges = null!;
    private NumericUpDown _numExtraCharges = null!;
    private NumericUpDown _numDiscount = null!;
    private Label _lblTotalAmount = null!;
    private TextBox _txtNotes = null!;
    private Button _btnCheckOut = null!;
    private Button _btnCancel = null!;

    public CheckOutForm(
        Room room,
        Booking booking,
        Customer? customer,
        Folio? folio,
        IBookingService bookingService)
    {
        _room = room;
        _booking = booking;
        _customer = customer;
        _folio = folio;
        _bookingService = bookingService;

        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = $"คืนห้องพัก / เช็คเอาท์ (Check-Out) — ห้อง {_room.RoomNumber}";
        Size = new Size(560, 540);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var checkInTime = _booking.CheckInActual ?? _booking.CheckInPlanned;
        var now = DateTime.Now;
        var durationSpan = now - checkInTime;
        var durationText = _booking.RatePlan == RatePlanType.Hourly
            ? $"{Math.Max(1, Math.Ceiling(durationSpan.TotalHours))} ชั่วโมง"
            : $"{Math.Max(1, Math.Ceiling(durationSpan.TotalDays))} คืน/วัน";

        decimal initialRoomCharges = _folio?.RoomCharges ?? 0;
        if (initialRoomCharges == 0)
        {
            var units = _booking.RatePlan == RatePlanType.Hourly
                ? (decimal)Math.Max(1, Math.Ceiling(durationSpan.TotalHours))
                : (decimal)Math.Max(1, Math.Ceiling(durationSpan.TotalDays));
            initialRoomCharges = units * _booking.AgreedRate;
        }

        var lblHeader = new Label
        {
            Text = $"สรุปค่าใช้จ่ายเช็คเอาท์ ห้อง {_room.RoomNumber}",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.DarkRed,
            Location = new Point(20, 15),
            AutoSize = true
        };

        _lblCustomer = new Label
        {
            Text = $"ผู้เข้าพัก: {(_customer != null ? _customer.FullName : "ไม่ระบุ")} (เบอร์: {_customer?.Phone ?? "-"})",
            Location = new Point(20, 55),
            Font = new Font("Segoe UI", 11F),
            AutoSize = true
        };

        _lblCheckInTime = new Label
        {
            Text = $"เวลาเช็คอิน: {checkInTime:dd/MM/yyyy HH:mm} น.",
            Location = new Point(20, 88),
            Font = new Font("Segoe UI", 11F),
            AutoSize = true
        };

        _lblDuration = new Label
        {
            Text = $"ระยะเวลาเข้าพักจริง: {durationText}",
            Location = new Point(20, 120),
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            AutoSize = true
        };

        var lblRoomCharges = new Label { Text = "ค่าห้องพัก (บาท):", Location = new Point(20, 165), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numRoomCharges = new NumericUpDown
        {
            Location = new Point(190, 162),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = initialRoomCharges
        };
        _numRoomCharges.ValueChanged += RecalculateTotal;

        var lblExtraCharges = new Label { Text = "ค่าบริการเสริม/มินิบาร์:", Location = new Point(20, 205), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numExtraCharges = new NumericUpDown
        {
            Location = new Point(190, 202),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = _folio?.ExtraCharges ?? 0
        };
        _numExtraCharges.ValueChanged += RecalculateTotal;

        var lblDiscount = new Label { Text = "ส่วนลด (บาท):", Location = new Point(20, 245), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numDiscount = new NumericUpDown
        {
            Location = new Point(190, 242),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = _folio?.DiscountAmount ?? 0
        };
        _numDiscount.ValueChanged += RecalculateTotal;

        var lblTotalHeader = new Label { Text = "ยอดสุทธิที่ต้องชำระ:", Location = new Point(20, 290), Font = new Font("Segoe UI", 12F, FontStyle.Bold), AutoSize = true };
        _lblTotalAmount = new Label
        {
            Text = "0.00 บาท",
            Location = new Point(200, 286),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.DarkGreen,
            AutoSize = true
        };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(20, 335), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(190, 332), Width = 320, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 55 };

        _btnCheckOut = new Button
        {
            Text = "ยืนยันเช็คเอาท์",
            BackColor = Color.Crimson,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point(140, 435),
            Size = new Size(135, 42)
        };
        _btnCheckOut.Click += BtnCheckOut_Click;

        var btnPrintReceipt = new Button
        {
            Text = "พิมพ์ใบเสร็จ",
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point(285, 435),
            Size = new Size(125, 42)
        };
        btnPrintReceipt.Click += (s, e) => PrintReceipt();

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 11F),
            Location = new Point(420, 435),
            Size = new Size(90, 42)
        };

        // ToolTips Onboarding Guide
        var tt = new ToolTip();
        tt.SetToolTip(_numRoomCharges, "ค่าห้องพักที่คำนวณตามระยะเวลาเข้าพักจริง (สามารถปรับเปลี่ยนได้หากมีส่วนลดพิเศษ)");
        tt.SetToolTip(_numExtraCharges, "ระบุค่าบริการเพิ่มเติม มินิบาร์ หรือค่าเสียหายย่อย (บาท)");
        tt.SetToolTip(_numDiscount, "ระบุจำนวนเงินส่วนลดสำหรับบิลนี้ (บาท)");
        tt.SetToolTip(_lblTotalAmount, "ยอดสุทธิทั้งหมดที่ต้องเรียกเก็บจากผู้เข้าพักก่อนคืนห้อง");
        tt.SetToolTip(_btnCheckOut, "ยืนยันการเช็คเอาท์ สรุปบิล และเปลี่ยนสถานะห้องเป็น 'รอทำความสะอาด'");
        tt.SetToolTip(btnPrintReceipt, "พิมพ์ใบเสร็จรับเงิน/ใบแจ้งหนี้พร้อมช่องเซ็นชื่อผู้เข้าพัก");
        tt.SetToolTip(_btnCancel, "ยกเลิกการเช็คเอาท์และกลับสู่หน้าหลัก");

        Controls.AddRange(new Control[]
        {
            lblHeader, _lblCustomer, _lblCheckInTime, _lblDuration,
            lblRoomCharges, _numRoomCharges, lblExtraCharges, _numExtraCharges,
            lblDiscount, _numDiscount, lblTotalHeader, _lblTotalAmount,
            lblNotes, _txtNotes, _btnCheckOut, btnPrintReceipt, _btnCancel
        });

        RecalculateTotal(null, EventArgs.Empty);

        AcceptButton = _btnCheckOut;
        CancelButton = _btnCancel;
    }

    private void RecalculateTotal(object? sender, EventArgs e)
    {
        var total = Math.Max(0, _numRoomCharges.Value + _numExtraCharges.Value - _numDiscount.Value);
        _lblTotalAmount.Text = $"{total:N2} บาท";
    }

    private void PrintReceipt()
    {
        var currentFolio = _folio ?? new Folio
        {
            RoomCharges = _numRoomCharges.Value,
            ExtraCharges = _numExtraCharges.Value,
            DiscountAmount = _numDiscount.Value,
            TotalAmount = Math.Max(0, _numRoomCharges.Value + _numExtraCharges.Value - _numDiscount.Value)
        };

        var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(
            "โรงแรม HotelPOS TH",
            "123/45 ถนนสุขุมวิท กรุงเทพฯ",
            "02-123-4567",
            "0105560000000",
            _booking,
            _room,
            _customer,
            currentFolio,
            "admin"
        );
        printer.ShowPrintPreview();
    }

    private async void BtnCheckOut_Click(object? sender, EventArgs e)
    {
        var total = Math.Max(0, _numRoomCharges.Value + _numExtraCharges.Value - _numDiscount.Value);
        var result = MessageBox.Show(
            $"ยืนยันการเช็คเอาท์ห้อง {_room.RoomNumber}?\nยอดสุทธิ: {total:N2} บาท\nสถานะห้องจะถูกเปลี่ยนเป็น 'รอทำความสะอาด'",
            "ยืนยันเช็คเอาท์",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        try
        {
            _btnCheckOut.Enabled = false;
            await _bookingService.CheckOutAsync(
                _booking.Id,
                _numExtraCharges.Value,
                _numDiscount.Value,
                _txtNotes.Text
            );

            var printAsk = MessageBox.Show(
                $"เช็คเอาท์ห้อง {_room.RoomNumber} เรียบร้อยแล้ว!\n\nคุณต้องการพิมพ์ใบเสร็จรับเงิน/ใบแจ้งหนี้พร้อมช่องเซ็นชื่อเลยหรือไม่?",
                "พิมพ์ใบเสร็จ",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (printAsk == DialogResult.Yes)
            {
                PrintReceipt();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการเช็คเอาท์: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnCheckOut.Enabled = true;
        }
    }
}
