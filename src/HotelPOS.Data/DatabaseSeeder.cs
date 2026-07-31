using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data;

public class DatabaseSeeder
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public DatabaseSeeder(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task ResetAndSeedDatabaseAsync(string billingMonth = "2026-07")
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _logger.Info(LogCategory.Database, "เริ่มดำเนินการล้างข้อมูลเดิมและจำลองชุดข้อมูลใหม่ครบทุกเคส", correlationId);

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Clear existing transactional data
            await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM utility_bills;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM meter_readings;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM payments;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM invoice_documents;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM sale_items;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM sales;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM folios;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM bookings;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM customers;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM rooms;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM room_types;", transaction: transaction);
            await connection.ExecuteAsync("DELETE FROM audit_logs;", transaction: transaction);
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;", transaction: transaction);

            // 2. Insert Room Types
            int standardTypeId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO room_types (name, daily_rate, hourly_rate, monthly_rate, description, electric_billing_mode, electric_flat_rate, water_billing_mode, water_flat_rate, color_hex)
                VALUES ('Standard Room', 500, 100, 3500, 'ห้องมาตรฐาน เตียงเดี่ยว', 0, 0, 0, 0, '#3B82F6') RETURNING id;", transaction: transaction);

            int deluxeTypeId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO room_types (name, daily_rate, hourly_rate, monthly_rate, description, electric_billing_mode, electric_flat_rate, water_billing_mode, water_flat_rate, color_hex)
                VALUES ('Deluxe Room', 700, 150, 4500, 'ห้องดีลักซ์ เตียงคู่ วิวสวย', 0, 0, 0, 0, '#8B5CF6') RETURNING id;", transaction: transaction);

            int suiteTypeId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO room_types (name, daily_rate, hourly_rate, monthly_rate, description, electric_billing_mode, electric_flat_rate, water_billing_mode, water_flat_rate, color_hex)
                VALUES ('Suite Room', 1200, 250, 6500, 'ห้องสูท กว้างขวาง คิดค่าไฟ/น้ำเหมาจ่าย', 1, 500, 1, 120, '#EC4899') RETURNING id;", transaction: transaction);

            // 3. Insert Rooms
            int r101 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('101', @TypeId, '1', 1, 'ห้องชั้น 1 ติดสวน') RETURNING id;", new { TypeId = standardTypeId }, transaction);
            int r102 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('102', @TypeId, '1', 1, 'ห้องชั้น 1 ริมสุด') RETURNING id;", new { TypeId = standardTypeId }, transaction);
            int r103 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('103', @TypeId, '1', 1, 'ห้องชั้น 1 เงียบสงบ') RETURNING id;", new { TypeId = standardTypeId }, transaction);
            int r104 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('104', @TypeId, '1', 1, 'ห้องดีลักซ์ชั้น 1') RETURNING id;", new { TypeId = deluxeTypeId }, transaction);
            int r105 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('105', @TypeId, '1', 0, 'ห้องว่างพร้อมขาย') RETURNING id;", new { TypeId = deluxeTypeId }, transaction);

            int r201 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('201', @TypeId, '2', 1, 'ห้องสูท VIP ชั้น 2') RETURNING id;", new { TypeId = suiteTypeId }, transaction);
            int r202 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('202', @TypeId, '2', 2, 'เช็คเอาท์แล้ว - รอแม่บ้านทำความสะอาด') RETURNING id;", new { TypeId = deluxeTypeId }, transaction);
            int r203 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('203', @TypeId, '2', 0, 'ห้องว่างพร้อมขาย') RETURNING id;", new { TypeId = standardTypeId }, transaction);
            int r204 = await connection.QuerySingleAsync<int>(@"INSERT INTO rooms (room_number, room_type_id, floor, status, notes) VALUES ('204', @TypeId, '2', 0, 'ห้องว่างพร้อมขาย') RETURNING id;", new { TypeId = standardTypeId }, transaction);

            // 4. Insert Customers
            int c1 = await connection.QuerySingleAsync<int>(@"INSERT INTO customers (full_name, phone, email, id_card_or_passport, address) VALUES ('คุณสมชาย ใจดี', '081-234-5678', 'somchai@email.com', '1100200300401', '123/45 กรุงเทพฯ') RETURNING id;", transaction);
            int c2 = await connection.QuerySingleAsync<int>(@"INSERT INTO customers (full_name, phone, email, id_card_or_passport, address) VALUES ('คุณวิภาวรรณ สุขเสริฐ', '089-876-5432', 'wipawan@email.com', '1100200300402', '45/67 นนทบุรี') RETURNING id;", transaction);
            int c3 = await connection.QuerySingleAsync<int>(@"INSERT INTO customers (full_name, phone, email, id_card_or_passport, address) VALUES ('คุณอนันต์ สุขใจ', '086-111-2233', 'anan@email.com', '1100200300403', '78/90 ปทุมธานี') RETURNING id;", transaction);
            int c4 = await connection.QuerySingleAsync<int>(@"INSERT INTO customers (full_name, phone, email, id_card_or_passport, address) VALUES ('คุณณัฐพงษ์ วงศ์สวัสดิ์', '090-555-6677', 'nattapong@email.com', '1100200300404', '88/99 สมุทรปราการ') RETURNING id;", transaction);
            int c5 = await connection.QuerySingleAsync<int>(@"INSERT INTO customers (full_name, phone, email, id_card_or_passport, address) VALUES ('คุณกิตติศักดิ์ มีสุข', '082-999-8877', 'kittisak@email.com', '1100200300405', '99/12 ชลบุรี') RETURNING id;", transaction);

            // 5. Insert Active Monthly Bookings
            async Task CreateMonthlyBooking(int roomId, int custId, string code, decimal rate)
            {
                int bkId = await connection.QuerySingleAsync<int>(@"
                    INSERT INTO bookings (booking_code, room_id, customer_id, rate_plan, check_in_planned, check_in_actual, status, agreed_rate)
                    VALUES (@Code, @RoomId, @CustId, 2, '2026-01-01 14:00:00', '2026-01-01 14:00:00', 1, @Rate) RETURNING id;",
                    new { Code = code, RoomId = roomId, CustId = custId, Rate = rate }, transaction);

                await connection.ExecuteAsync(@"
                    INSERT INTO folios (booking_id, is_closed, room_charges, total_amount)
                    VALUES (@BkId, 0, @Rate, @Rate);", new { BkId = bkId, Rate = rate }, transaction);
            }

            await CreateMonthlyBooking(r101, c1, "BK-M101", 3500m);
            await CreateMonthlyBooking(r102, c2, "BK-M102", 3500m);
            await CreateMonthlyBooking(r103, c3, "BK-M103", 3500m);
            await CreateMonthlyBooking(r104, c4, "BK-M104", 4500m);
            await CreateMonthlyBooking(r201, c5, "BK-M201", 6500m);

            // 6. Seed Utility Readings & Bills for Billing Month
            
            // --- เคส 1: ห้อง 101 (บันทึกมิเตอร์ + ออกบิลแล้ว + ชำระแล้ว สีเขียว) ---
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 0, @Month, 1200, 1350, 150, 8.00, 1200);", new { RoomId = r101, Month = billingMonth }, transaction);
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 1, @Month, 450, 465, 15, 18.00, 270);", new { RoomId = r101, Month = billingMonth }, transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO utility_bills (bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, electric_billing_mode, water_prev, water_curr, water_units, water_rate, water_amount, water_billing_mode, water_person_count, common_area_fee, garbage_fee, extra_charges, discount_amount, total_amount, is_paid, paid_at, payment_method, notes)
                VALUES ('UB-202607-0001', @RoomId, @Month, 3500, 1200, 1350, 150, 8.00, 1200, 'METER', 450, 465, 15, 18.00, 270, 'METER', 1, 0, 0, 0, 0, 4970, 1, datetime('now', 'localtime'), 0, 'ชำระเงินสดเรียบร้อย');",
                new { RoomId = r101, Month = billingMonth }, transaction);

            // --- เคส 2: ห้อง 102 (บันทึกมิเตอร์ + ออกบิลแล้ว + ค้างชำระ สีแดง) ---
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 0, @Month, 1100, 1280, 180, 8.00, 1440);", new { RoomId = r102, Month = billingMonth }, transaction);
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 1, @Month, 380, 400, 20, 18.00, 360);", new { RoomId = r102, Month = billingMonth }, transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO utility_bills (bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, electric_billing_mode, water_prev, water_curr, water_units, water_rate, water_amount, water_billing_mode, water_person_count, common_area_fee, garbage_fee, extra_charges, discount_amount, total_amount, is_paid, notes)
                VALUES ('UB-202607-0002', @RoomId, @Month, 3500, 1100, 1280, 180, 8.00, 1440, 'METER', 380, 400, 20, 18.00, 360, 'METER', 1, 0, 0, 0, 0, 5300, 0, 'รอนำเงินสดมาชำระ');",
                new { RoomId = r102, Month = billingMonth }, transaction);

            // --- เคส 3: ห้อง 201 (เหมาจ่าย FLAT Mode + ออกบิลแล้ว + ค้างชำระ) ---
            await connection.ExecuteAsync(@"
                INSERT INTO utility_bills (bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, electric_billing_mode, water_prev, water_curr, water_units, water_rate, water_amount, water_billing_mode, water_person_count, common_area_fee, garbage_fee, extra_charges, discount_amount, total_amount, is_paid, notes)
                VALUES ('UB-202607-0003', @RoomId, @Month, 6500, 0, 0, 0, 0, 500, 'FLAT', 0, 0, 0, 0, 240, 'FLAT', 2, 0, 0, 0, 0, 7240, 0, 'โหมดเหมาจ่าย (2 คน)');",
                new { RoomId = r201, Month = billingMonth }, transaction);

            // --- เคส 4: ห้อง 103, 104 (บันทึกมิเตอร์เดือนก่อนหน้า 2026-06 ไว้ รอคีย์มิเตอร์เดือนปัจจุบัน 2026-07) ---
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 0, '2026-06', 1050, 1200, 150, 8.00, 1200);", new { RoomId = r103 }, transaction);
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 1, '2026-06', 390, 410, 20, 18.00, 360);", new { RoomId = r103 }, transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 0, '2026-06', 1400, 1600, 200, 8.00, 1600);", new { RoomId = r104 }, transaction);
            await connection.ExecuteAsync(@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount)
                VALUES (@RoomId, 1, '2026-06', 500, 525, 25, 18.00, 450);", new { RoomId = r104 }, transaction);

            transaction.Commit();
            _logger.Info(LogCategory.Database, "ล้างข้อมูลเดิมและสร้างชุดข้อมูลจำลองใหม่ทุกเคสเรียบร้อยสมบูรณ์", correlationId);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.Error(LogCategory.Database, "เกิดข้อผิดพลาดในการล้างและจำลองชุดข้อมูลใหม่", ex, correlationId);
            throw;
        }
    }
}
