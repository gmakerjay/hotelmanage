using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class UtilityBillService : IUtilityBillService
{
    private readonly IMeterReadingRepository _meterRepo;
    private readonly IUtilityBillRepository _billRepo;
    private readonly ISettingsService _settingsService;
    private readonly IRoomRepository _roomRepo;
    private readonly IAppLogger _logger;

    public UtilityBillService(
        IMeterReadingRepository meterRepo,
        IUtilityBillRepository billRepo,
        ISettingsService settingsService,
        IRoomRepository roomRepo,
        IAppLogger logger)
    {
        _meterRepo = meterRepo;
        _billRepo = billRepo;
        _settingsService = settingsService;
        _roomRepo = roomRepo;
        _logger = logger;
    }

    public async Task<int> RecordMeterReadingAsync(int roomId, UtilityType type, decimal prevReading, decimal currReading, string billingMonth, string? notes = null)
    {
        var correlationId = _logger.NewCorrelationId();

        // Validation: เลข "หลัง" ต้องไม่น้อยกว่า "ก่อน"
        if (currReading < prevReading)
            throw new ArgumentException($"เลขมิเตอร์หลัง ({currReading}) น้อยกว่ามิเตอร์ก่อน ({prevReading}) ไม่ได้");

        var settings = await _settingsService.GetAllSettingsAsync();
        decimal ratePerUnit = type == UtilityType.Electric
            ? settings.ElectricRatePerUnit
            : settings.WaterRatePerUnit;

        decimal unitsUsed = currReading - prevReading;
        decimal totalAmount = unitsUsed * ratePerUnit;

        var reading = new MeterReading
        {
            RoomId = roomId,
            UtilityType = type,
            BillingMonth = billingMonth,
            ReadingPrev = prevReading,
            ReadingCurr = currReading,
            UnitsUsed = unitsUsed,
            RatePerUnit = ratePerUnit,
            TotalAmount = totalAmount,
            Notes = notes
        };

        int id = await _meterRepo.UpsertAsync(reading);
        _logger.Info(LogCategory.Utility, $"บันทึกมิเตอร์ {type} ห้อง {roomId} เดือน {billingMonth}: {prevReading} → {currReading} = {unitsUsed} หน่วย × {ratePerUnit} = {totalAmount} บาท", correlationId);
        return id;
    }

    public async Task<UtilityBill> GenerateMonthlyBillAsync(int roomId, string billingMonth, int waterPersonCount = 1, decimal extraCharges = 0, decimal discount = 0, string? notes = null)
    {
        var correlationId = _logger.NewCorrelationId();
        var settings = await _settingsService.GetAllSettingsAsync();

        // ดึงข้อมูลห้อง + ค่าเช่า
        var room = await _roomRepo.GetRoomByIdAsync(roomId);
        if (room == null) throw new ArgumentException($"ไม่พบห้อง ID={roomId}");

        var roomType = await _roomRepo.GetRoomTypeByIdAsync(room.RoomTypeId);
        decimal roomCharge = roomType?.MonthlyRate ?? 0;

        // ดึง meter readings ของเดือนนี้
        var readings = await _meterRepo.GetByRoomAndMonthAsync(roomId, billingMonth);
        var electricReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
        var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

        decimal electricAmount = electricReading?.TotalAmount ?? 0;

        // คำนวณค่าน้ำตามโหมด
        decimal waterAmount;
        string waterBillingMode = settings.WaterBillingMode;
        if (waterBillingMode == "FLAT")
        {
            // เหมาจ่ายรายคน
            waterAmount = settings.WaterFlatRatePerPerson * waterPersonCount;
        }
        else
        {
            // ตามมิเตอร์
            waterAmount = waterReading?.TotalAmount ?? 0;
        }

        decimal commonAreaFee = settings.CommonAreaFee;
        decimal garbageFee = settings.GarbageFee;

        decimal totalAmount = roomCharge + electricAmount + waterAmount + commonAreaFee + garbageFee + extraCharges - discount;

        // ตรวจสอบว่ามีบิลเดิมอยู่แล้วหรือไม่
        var existingBill = await _billRepo.GetByRoomAndMonthAsync(roomId, billingMonth);

        var bill = existingBill ?? new UtilityBill();
        if (bill.Id == 0)
        {
            bill.BillCode = await _billRepo.GenerateNextBillCodeAsync(billingMonth);
        }

        bill.RoomId = roomId;
        bill.BillingMonth = billingMonth;
        bill.RoomCharge = roomCharge;

        bill.ElectricPrev = electricReading?.ReadingPrev ?? 0;
        bill.ElectricCurr = electricReading?.ReadingCurr ?? 0;
        bill.ElectricUnits = electricReading?.UnitsUsed ?? 0;
        bill.ElectricRate = electricReading?.RatePerUnit ?? settings.ElectricRatePerUnit;
        bill.ElectricAmount = electricAmount;

        bill.WaterPrev = waterReading?.ReadingPrev ?? 0;
        bill.WaterCurr = waterReading?.ReadingCurr ?? 0;
        bill.WaterUnits = waterReading?.UnitsUsed ?? 0;
        bill.WaterRate = waterReading?.RatePerUnit ?? settings.WaterRatePerUnit;
        bill.WaterAmount = waterAmount;

        bill.WaterBillingMode = waterBillingMode;
        bill.WaterPersonCount = waterPersonCount;
        bill.CommonAreaFee = commonAreaFee;
        bill.GarbageFee = garbageFee;
        bill.ExtraCharges = extraCharges;
        bill.DiscountAmount = discount;
        bill.TotalAmount = totalAmount;
        bill.Notes = notes;
        bill.RoomNumber = room.RoomNumber;
        bill.ElectricReading = electricReading;
        bill.WaterReading = waterReading;

        bill.Id = await _billRepo.SaveAsync(bill);
        _logger.Info(LogCategory.Utility, $"สร้างใบแจ้งหนี้ {bill.BillCode} ห้อง {room.RoomNumber} เดือน {billingMonth}: รวม {totalAmount} บาท", correlationId);
        return bill;
    }

    public async Task<UtilityBill> GetMonthlyBillPreviewAsync(int roomId, string billingMonth, int waterPersonCount = 1)
    {
        var settings = await _settingsService.GetAllSettingsAsync();
        var room = await _roomRepo.GetRoomByIdAsync(roomId);
        if (room == null) throw new ArgumentException($"ไม่พบห้อง ID={roomId}");

        var roomType = await _roomRepo.GetRoomTypeByIdAsync(room.RoomTypeId);
        decimal roomCharge = roomType?.MonthlyRate ?? 0;

        var readings = await _meterRepo.GetByRoomAndMonthAsync(roomId, billingMonth);
        var electricReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
        var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

        decimal electricAmount = electricReading?.TotalAmount ?? 0;
        string waterBillingMode = settings.WaterBillingMode;
        decimal waterAmount = waterBillingMode == "FLAT"
            ? settings.WaterFlatRatePerPerson * waterPersonCount
            : waterReading?.TotalAmount ?? 0;

        decimal commonAreaFee = settings.CommonAreaFee;
        decimal garbageFee = settings.GarbageFee;
        decimal totalAmount = roomCharge + electricAmount + waterAmount + commonAreaFee + garbageFee;

        return new UtilityBill
        {
            RoomId = roomId,
            BillingMonth = billingMonth,
            RoomCharge = roomCharge,
            ElectricAmount = electricAmount,
            WaterAmount = waterAmount,
            WaterBillingMode = waterBillingMode,
            WaterPersonCount = waterPersonCount,
            CommonAreaFee = commonAreaFee,
            GarbageFee = garbageFee,
            TotalAmount = totalAmount,
            RoomNumber = room.RoomNumber,
            ElectricReading = electricReading,
            WaterReading = waterReading
        };
    }

    public async Task MarkBillAsPaidAsync(int billId, PaymentMethod paymentMethod)
    {
        await _billRepo.MarkAsPaidAsync(billId, paymentMethod);
    }

    public async Task<IEnumerable<UtilityBill>> GetBillsByMonthAsync(string billingMonth)
    {
        return await _billRepo.GetByMonthAsync(billingMonth);
    }

    public async Task<IEnumerable<MeterReading>> GetMeterReadingsByMonthAsync(string billingMonth)
    {
        return await _meterRepo.GetByMonthAsync(billingMonth);
    }

    public async Task<decimal> GetPreviousMeterValueAsync(int roomId, UtilityType type, string currentBillingMonth)
    {
        var previous = await _meterRepo.GetPreviousReadingAsync(roomId, type, currentBillingMonth);
        return previous?.ReadingCurr ?? 0; // เลข "หลัง" ของเดือนก่อน = เลข "ก่อน" ของเดือนนี้
    }

    public async Task<IEnumerable<UtilityBill>> GetBillHistoryAsync(int roomId, int lastNMonths = 12)
    {
        return await _billRepo.GetHistoryAsync(roomId, lastNMonths);
    }

    public async Task<IEnumerable<MeterReading>> GetMeterHistoryAsync(int roomId, int lastNMonths = 12)
    {
        return await _meterRepo.GetHistoryAsync(roomId, lastNMonths);
    }
}
