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
    private readonly IUtilityBillService? _utilityBillService;
    private readonly ISettingsService? _settingsService;

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
        IBookingService bookingService,
        IUtilityBillService? utilityBillService = null,
        ISettingsService? settingsService = null)
    {
        _room = room;
        _booking = booking;
        _customer = customer;
        _folio = folio;
        _bookingService = bookingService;
        _utilityBillService = utilityBillService;
        _settingsService = settingsService;

        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = $"คืนห้องพัก / เช็คเอาท์ (Check-Out) — ห้อง {_room.RoomNumber}";
        Size = new Size(620, 550);
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
            Location = new Point(20, 52),
            Font = new Font("Segoe UI", 11F),
            AutoSize = true
        };

        _lblCheckInTime = new Label
        {
            Text = $"เวลาเช็คอิน: {checkInTime:dd/MM/yyyy HH:mm} น.",
            Location = new Point(20, 82),
            Font = new Font("Segoe UI", 11F),
            AutoSize = true
        };

        _lblDuration = new Label
        {
            Text = $"ระยะเวลาเข้าพักจริง: {durationText}",
            Location = new Point(20, 112),
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            AutoSize = true
        };

        var lblRoomCharges = new Label { Text = "ค่าห้องพัก (บาท):", Location = new Point(20, 155), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numRoomCharges = new NumericUpDown
        {
            Location = new Point(190, 152),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = initialRoomCharges
        };
        _numRoomCharges.ValueChanged += RecalculateTotal;

        var lblExtraCharges = new Label { Text = "ค่าบริการเสริม/มินิบาร์:", Location = new Point(20, 195), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numExtraCharges = new NumericUpDown
        {
            Location = new Point(190, 192),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = _folio?.ExtraCharges ?? 0
        };
        _numExtraCharges.ValueChanged += RecalculateTotal;

        var btnMeterReading = new Button
        {
            Text = "กรอกค่าน้ำ-ค่าไฟ",
            BackColor = Color.FromArgb(14, 116, 144),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(380, 191),
            Size = new Size(190, 32),
            Cursor = Cursors.Hand
        };
        btnMeterReading.FlatAppearance.BorderSize = 0;
        btnMeterReading.Click += async (s, e) => await OpenMeterReadingDialogAsync();

        var lblDiscount = new Label { Text = "ส่วนลด (บาท):", Location = new Point(20, 235), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numDiscount = new NumericUpDown
        {
            Location = new Point(190, 232),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = _folio?.DiscountAmount ?? 0
        };
        _numDiscount.ValueChanged += RecalculateTotal;

        var lblTotalHeader = new Label { Text = "ยอดสุทธิที่ต้องชำระ:", Location = new Point(20, 280), Font = new Font("Segoe UI", 12F, FontStyle.Bold), AutoSize = true };
        _lblTotalAmount = new Label
        {
            Text = "0.00 บาท",
            Location = new Point(200, 276),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.DarkGreen,
            AutoSize = true
        };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(20, 325), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(190, 322), Width = 380, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 55 };

        _btnCheckOut = new Button
        {
            Text = "ยืนยันเช็คเอาท์",
            BackColor = Color.Crimson,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(20, 425),
            Size = new Size(140, 42),
            Cursor = Cursors.Hand
        };
        _btnCheckOut.Click += BtnCheckOut_Click;

        var btnPrintReceipt = new Button
        {
            Text = "พิมพ์สลิป (Receipt)",
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(170, 425),
            Size = new Size(150, 42),
            Cursor = Cursors.Hand
        };
        btnPrintReceipt.Click += (s, e) => PrintReceipt("80mm");

        var btnPrintA4 = new Button
        {
            Text = "พิมพ์ใบแจ้งหนี้ (A4)",
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(330, 425),
            Size = new Size(155, 42),
            Cursor = Cursors.Hand
        };
        btnPrintA4.Click += (s, e) => PrintReceipt("A4");

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 10F),
            Location = new Point(495, 425),
            Size = new Size(80, 42)
        };

        Controls.AddRange(new Control[]
        {
            lblHeader, _lblCustomer, _lblCheckInTime, _lblDuration,
            lblRoomCharges, _numRoomCharges, lblExtraCharges, _numExtraCharges, btnMeterReading,
            lblDiscount, _numDiscount, lblTotalHeader, _lblTotalAmount,
            lblNotes, _txtNotes, _btnCheckOut, btnPrintReceipt, btnPrintA4, _btnCancel
        });

        RecalculateTotal(null, EventArgs.Empty);

        AcceptButton = _btnCheckOut;
        CancelButton = _btnCancel;
    }

    private async Task OpenMeterReadingDialogAsync()
    {
        if (_utilityBillService == null || _settingsService == null)
        {
            MessageBox.Show("ระบบค่าน้ำค่าไฟไม่ได้ถูกเชื่อมต่อในฟอร์มนี้", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string billingMonth = DateTime.Now.ToString("yyyy-MM");
            var settings = await _settingsService.GetAllSettingsAsync();
            var roomType = _room;

            decimal elecPrev = await _utilityBillService.GetPreviousMeterValueAsync(_room.Id, UtilityType.Electric, billingMonth);
            decimal waterPrev = await _utilityBillService.GetPreviousMeterValueAsync(_room.Id, UtilityType.Water, billingMonth);

            var readings = (await _utilityBillService.GetMeterReadingsByMonthAsync(billingMonth))
                .Where(r => r.RoomId == _room.Id).ToList();

            var elecReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
            var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

            decimal elecCurr = elecReading?.ReadingCurr ?? 0;
            decimal waterCurr = waterReading?.ReadingCurr ?? 0;
            if (elecReading != null) elecPrev = elecReading.ReadingPrev;
            if (waterReading != null) waterPrev = waterReading.ReadingPrev;

            using var dlg = new MeterReadingInputDialog(
                _room,
                _customer?.FullName ?? "ผู้เช่ารายเดือน",
                billingMonth,
                _numRoomCharges.Value,
                elecPrev,
                elecCurr,
                waterPrev,
                waterCurr,
                1,
                _numExtraCharges.Value,
                _numDiscount.Value,
                _txtNotes.Text,
                settings
            );

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await _utilityBillService.RecordMeterReadingAsync(
                    _room.Id,
                    UtilityType.Electric,
                    dlg.ElecPrev,
                    dlg.ElecCurr,
                    billingMonth,
                    dlg.Notes
                );

                if (settings.WaterBillingMode == "METER")
                {
                    await _utilityBillService.RecordMeterReadingAsync(
                        _room.Id,
                        UtilityType.Water,
                        dlg.WaterPrev,
                        dlg.WaterCurr,
                        billingMonth,
                        dlg.Notes
                    );
                }

                var bill = await _utilityBillService.GenerateMonthlyBillAsync(_room.Id, billingMonth, dlg.WaterPersons);

                if (dlg.PrintBillRequested)
                {
                    var printer = new HotelPOS.Printing.UtilityInvoicePrinter(bill, _customer, settings);
                    printer.ShowPrintPreview();
                }

                MessageBox.Show($"บันทึกค่าน้ำ-ค่าไฟห้อง {_room.RoomNumber} เรียบร้อยแล้ว (ยอดรวมค่าน้ำไฟ: {bill.ElectricAmount + bill.WaterAmount:N2} บาท)", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถเปิดการบันทึกค่าน้ำค่าไฟได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecalculateTotal(object? sender, EventArgs e)
    {
        var total = Math.Max(0, _numRoomCharges.Value + _numExtraCharges.Value - _numDiscount.Value);
        _lblTotalAmount.Text = $"{total:N2} บาท";
    }

    private async void PrintReceipt(string paperType = "")
    {
        var currentFolio = _folio ?? new Folio
        {
            RoomCharges = _numRoomCharges.Value,
            ExtraCharges = _numExtraCharges.Value,
            DiscountAmount = _numDiscount.Value,
            TotalAmount = Math.Max(0, _numRoomCharges.Value + _numExtraCharges.Value - _numDiscount.Value)
        };

        SystemSettingsDto? settings = null;
        if (_settingsService != null)
        {
            try
            {
                settings = await _settingsService.GetAllSettingsAsync();
            }
            catch { }
        }
        settings ??= new SystemSettingsDto();

        if (!string.IsNullOrEmpty(paperType))
        {
            settings = new SystemSettingsDto
            {
                ShopName = settings.ShopName,
                ShopAddress = settings.ShopAddress,
                ShopPhone = settings.ShopPhone,
                ShopTaxId = settings.ShopTaxId,
                BillHeader = settings.BillHeader,
                BillFooter = settings.BillFooter,
                PrinterName = settings.PrinterName,
                PaperType = paperType,
                ShowSignatureBox = settings.ShowSignatureBox,
                LogoImagePath = settings.LogoImagePath,
                QrCodeImagePath = settings.QrCodeImagePath,
                ElectricBillingMode = settings.ElectricBillingMode,
                ElectricRatePerUnit = settings.ElectricRatePerUnit,
                ElectricFlatRate = settings.ElectricFlatRate,
                WaterBillingMode = settings.WaterBillingMode,
                WaterRatePerUnit = settings.WaterRatePerUnit,
                WaterFlatRatePerPerson = settings.WaterFlatRatePerPerson,
                CommonAreaFee = settings.CommonAreaFee,
                GarbageFee = settings.GarbageFee
            };
        }

        UtilityBill? utilityBill = null;
        if (_utilityBillService != null && _booking.RatePlan == RatePlanType.Monthly)
        {
            try
            {
                var checkoutDate = _booking.CheckOutActual ?? DateTime.Now;
                string billingMonth = checkoutDate.ToString("yyyy-MM");
                var bills = await _utilityBillService.GetBillsByMonthAsync(billingMonth);
                utilityBill = bills.FirstOrDefault(b => b.RoomId == _booking.RoomId);
            }
            catch { }
        }

        if (utilityBill != null && paperType == "A4")
        {
            var printer = new HotelPOS.Printing.UtilityInvoicePrinter(utilityBill, _customer, settings);
            printer.ShowPrintPreview();
        }
        else
        {
            var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(
                settings.ShopName,
                settings.ShopAddress,
                settings.ShopPhone,
                settings.ShopTaxId,
                _booking,
                _room,
                _customer,
                currentFolio,
                "admin",
                settings,
                utilityBill
            );
            printer.ShowPrintPreview();
        }
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
