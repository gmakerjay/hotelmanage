using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class UtilityBillRepository : IUtilityBillRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public UtilityBillRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<UtilityBill?> GetByRoomAndMonthAsync(int roomId, string billingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount, ub.electric_billing_mode AS ElectricBillingMode,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.room_id = @RoomId AND ub.billing_month = @BillingMonth";
            return await conn.QuerySingleOrDefaultAsync<UtilityBill>(sql, new { RoomId = roomId, BillingMonth = billingMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงใบแจ้งหนี้ห้อง {roomId} เดือน {billingMonth} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<UtilityBill>> GetByMonthAsync(string billingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount, ub.electric_billing_mode AS ElectricBillingMode,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.billing_month = @BillingMonth
                ORDER BY r.room_number";
            return await conn.QueryAsync<UtilityBill>(sql, new { BillingMonth = billingMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงใบแจ้งหนี้ทุกห้องเดือน {billingMonth} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<UtilityBill>> GetPaidBillsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount, ub.electric_billing_mode AS ElectricBillingMode,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.is_paid = 1 AND ub.paid_at >= @StartDate AND ub.paid_at <= @EndDate
                ORDER BY ub.paid_at ASC";
            return await conn.QueryAsync<UtilityBill>(sql, new { StartDate = startDate.ToString("yyyy-MM-dd 00:00:00"), EndDate = endDate.ToString("yyyy-MM-dd 23:59:59") });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงใบแจ้งหนี้ที่ชำระแล้วในช่วง {startDate:yyyy-MM-dd} ถึง {endDate:yyyy-MM-dd} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<UtilityBill?> GetByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount, ub.electric_billing_mode AS ElectricBillingMode,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.id = @Id";
            return await conn.QuerySingleOrDefaultAsync<UtilityBill>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงใบแจ้งหนี้ ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveAsync(UtilityBill bill)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();

            if (bill.Id == 0)
            {
                // INSERT ใหม่
                const string sql = @"
                    INSERT INTO utility_bills (
                        bill_code, room_id, billing_month, room_charge,
                        electric_prev, electric_curr, electric_units, electric_rate, electric_amount, electric_billing_mode,
                        water_prev, water_curr, water_units, water_rate, water_amount,
                        water_billing_mode, water_person_count, common_area_fee, garbage_fee,
                        extra_charges, discount_amount, total_amount, is_paid, paid_at, payment_method, created_by, notes
                    ) VALUES (
                        @BillCode, @RoomId, @BillingMonth, @RoomCharge,
                        @ElectricPrev, @ElectricCurr, @ElectricUnits, @ElectricRate, @ElectricAmount, @ElectricBillingMode,
                        @WaterPrev, @WaterCurr, @WaterUnits, @WaterRate, @WaterAmount,
                        @WaterBillingMode, @WaterPersonCount, @CommonAreaFee, @GarbageFee,
                        @ExtraCharges, @DiscountAmount, @TotalAmount, @IsPaid, @PaidAt, @PaymentMethod, @CreatedBy, @Notes
                    )
                    ON CONFLICT(room_id, billing_month) DO UPDATE SET
                        room_charge = excluded.room_charge,
                        electric_prev = excluded.electric_prev,
                        electric_curr = excluded.electric_curr,
                        electric_units = excluded.electric_units,
                        electric_rate = excluded.electric_rate,
                        electric_amount = excluded.electric_amount,
                        electric_billing_mode = excluded.electric_billing_mode,
                        water_prev = excluded.water_prev,
                        water_curr = excluded.water_curr,
                        water_units = excluded.water_units,
                        water_rate = excluded.water_rate,
                        water_amount = excluded.water_amount,
                        water_billing_mode = excluded.water_billing_mode,
                        water_person_count = excluded.water_person_count,
                        common_area_fee = excluded.common_area_fee,
                        garbage_fee = excluded.garbage_fee,
                        extra_charges = excluded.extra_charges,
                        discount_amount = excluded.discount_amount,
                        total_amount = excluded.total_amount,
                        notes = excluded.notes
                    RETURNING id";
                bill.Id = await conn.ExecuteScalarAsync<int>(sql, new
                {
                    bill.BillCode, bill.RoomId, bill.BillingMonth, bill.RoomCharge,
                    bill.ElectricPrev, bill.ElectricCurr, bill.ElectricUnits, bill.ElectricRate, bill.ElectricAmount, bill.ElectricBillingMode,
                    bill.WaterPrev, bill.WaterCurr, bill.WaterUnits, bill.WaterRate, bill.WaterAmount,
                    bill.WaterBillingMode, bill.WaterPersonCount,
                    bill.CommonAreaFee, bill.GarbageFee,
                    bill.ExtraCharges, bill.DiscountAmount, bill.TotalAmount,
                    IsPaid = bill.IsPaid ? 1 : 0,
                    PaidAt = bill.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    PaymentMethod = bill.PaymentMethod.HasValue ? (int?)bill.PaymentMethod.Value : null,
                    bill.CreatedBy, bill.Notes
                });
                _logger.Info(LogCategory.Utility, $"สร้าง/อัปเดตใบแจ้งหนี้ {bill.BillCode} ห้อง {bill.RoomId} สำเร็จ (ID={bill.Id})", correlationId);
            }
            else
            {
                // UPDATE
                const string sql = @"
                    UPDATE utility_bills SET
                        room_charge = @RoomCharge,
                        electric_prev = @ElectricPrev, electric_curr = @ElectricCurr, electric_units = @ElectricUnits, electric_rate = @ElectricRate, electric_amount = @ElectricAmount, electric_billing_mode = @ElectricBillingMode,
                        water_prev = @WaterPrev, water_curr = @WaterCurr, water_units = @WaterUnits, water_rate = @WaterRate, water_amount = @WaterAmount,
                        water_billing_mode = @WaterBillingMode, water_person_count = @WaterPersonCount,
                        common_area_fee = @CommonAreaFee, garbage_fee = @GarbageFee,
                        extra_charges = @ExtraCharges, discount_amount = @DiscountAmount, total_amount = @TotalAmount,
                        notes = @Notes
                    WHERE id = @Id";
                await conn.ExecuteAsync(sql, new
                {
                    bill.Id, bill.RoomCharge,
                    bill.ElectricPrev, bill.ElectricCurr, bill.ElectricUnits, bill.ElectricRate, bill.ElectricAmount, bill.ElectricBillingMode,
                    bill.WaterPrev, bill.WaterCurr, bill.WaterUnits, bill.WaterRate, bill.WaterAmount,
                    bill.WaterBillingMode, bill.WaterPersonCount,
                    bill.CommonAreaFee, bill.GarbageFee,
                    bill.ExtraCharges, bill.DiscountAmount, bill.TotalAmount, bill.Notes
                });
                _logger.Info(LogCategory.Utility, $"อัปเดตใบแจ้งหนี้ {bill.BillCode} สำเร็จ (ID={bill.Id})", correlationId);
            }
            return bill.Id;
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"บันทึกใบแจ้งหนี้ห้อง {bill.RoomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task MarkAsPaidAsync(int billId, PaymentMethod paymentMethod)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE utility_bills SET
                    is_paid = 1,
                    paid_at = datetime('now', 'localtime'),
                    payment_method = @PaymentMethod
                WHERE id = @Id";
            await conn.ExecuteAsync(sql, new { Id = billId, PaymentMethod = (int)paymentMethod });
            _logger.Info(LogCategory.Utility, $"บันทึกชำระใบแจ้งหนี้ ID={billId} สำเร็จ ({paymentMethod})", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"บันทึกชำระใบแจ้งหนี้ ID={billId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<string> GenerateNextBillCodeAsync(string billingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            string prefix = $"UB-{billingMonth.Replace("-", "")}-";
            const string sql = @"
                SELECT bill_code FROM utility_bills
                WHERE bill_code LIKE @Prefix || '%'
                ORDER BY bill_code DESC LIMIT 1";
            var lastCode = await conn.QuerySingleOrDefaultAsync<string>(sql, new { Prefix = prefix });

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                string numPart = lastCode[(prefix.Length)..];
                if (int.TryParse(numPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }
            return $"{prefix}{nextNumber:D4}";
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"สร้างเลขที่บิลถัดไปเดือน {billingMonth} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<UtilityBill>> GetHistoryAsync(int roomId, int lastNMonths = 12)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            string cutoffMonth = DateTime.Now.AddMonths(-lastNMonths).ToString("yyyy-MM");
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.room_id = @RoomId AND ub.billing_month >= @CutoffMonth
                ORDER BY ub.billing_month DESC";
            return await conn.QueryAsync<UtilityBill>(sql, new { RoomId = roomId, CutoffMonth = cutoffMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงประวัติใบแจ้งหนี้ห้อง {roomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<UtilityBill>> GetAllUnpaidBillsAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT ub.id AS Id, ub.bill_code AS BillCode, ub.room_id AS RoomId,
                       ub.billing_month AS BillingMonth, ub.room_charge AS RoomCharge,
                       ub.electric_prev AS ElectricPrev, ub.electric_curr AS ElectricCurr,
                       ub.electric_units AS ElectricUnits, ub.electric_rate AS ElectricRate,
                       ub.electric_amount AS ElectricAmount, ub.electric_billing_mode AS ElectricBillingMode,
                       ub.water_prev AS WaterPrev, ub.water_curr AS WaterCurr,
                       ub.water_units AS WaterUnits, ub.water_rate AS WaterRate,
                       ub.water_amount AS WaterAmount,
                       ub.water_billing_mode AS WaterBillingMode, ub.water_person_count AS WaterPersonCount,
                       ub.common_area_fee AS CommonAreaFee, ub.garbage_fee AS GarbageFee,
                       ub.extra_charges AS ExtraCharges, ub.discount_amount AS DiscountAmount,
                       ub.total_amount AS TotalAmount, ub.is_paid AS IsPaid, ub.paid_at AS PaidAt,
                       ub.payment_method AS PaymentMethod, ub.created_by AS CreatedBy,
                       ub.created_at AS CreatedAt, ub.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM utility_bills ub
                JOIN rooms r ON r.id = ub.room_id
                WHERE ub.is_paid = 0
                ORDER BY ub.created_at ASC";
            return await conn.QueryAsync<UtilityBill>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, "ดึงใบแจ้งหนี้ที่ค้างชำระทั้งหมดล้มเหลว", ex, correlationId);
            return Enumerable.Empty<UtilityBill>();
        }
    }

    public async Task MarkAllUnpaidBillsAsPaidForRoomAsync(int roomId, PaymentMethod paymentMethod)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE utility_bills SET
                    is_paid = 1,
                    paid_at = datetime('now', 'localtime'),
                    payment_method = @PaymentMethod
                WHERE room_id = @RoomId AND is_paid = 0";
            await conn.ExecuteAsync(sql, new { RoomId = roomId, PaymentMethod = (int)paymentMethod });
            _logger.Info(LogCategory.Utility, $"บันทึกชำระเงินและเคลียร์บิลค้างชำระทั้งหมดของห้อง ID={roomId} สำเร็จ ({paymentMethod})", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"เคลียร์บิลค้างชำระของห้อง {roomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
