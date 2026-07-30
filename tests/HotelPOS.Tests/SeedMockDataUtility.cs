using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Data;
using Xunit;
using Dapper;

namespace HotelPOS.Tests;

public class SeedMockDataUtility
{
    [Fact]
    public async Task SeedDataIntoActiveDatabase()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbFolder = Path.Combine(appData, "PSoftRestRentManager");
        var dbPath = Path.Combine(dbFolder, "restrent.db");
        
        if (!Directory.Exists(dbFolder))
        {
            Directory.CreateDirectory(dbFolder);
        }

        var connectionFactory = new DbConnectionFactory(dbPath);
        var logFolder = Path.Combine(dbFolder, "logs");
        var appLogger = new HotelPOS.Logging.AppLogger(logFolder);
        var migrationRunner = new MigrationRunner(connectionFactory, appLogger);
        migrationRunner.EnsureDatabaseIsReady();

        using var conn = connectionFactory.CreateConnection();
        await conn.OpenAsync();

        // 1. Re-create masters (Clean up old data to prevent key conflicts)
        await conn.ExecuteAsync(@"
            DELETE FROM backup_history;
            DELETE FROM audit_logs;
            DELETE FROM utility_bills;
            DELETE FROM meter_readings;
            DELETE FROM payments;
            DELETE FROM sale_items;
            DELETE FROM sales;
            DELETE FROM folios;
            DELETE FROM bookings;
            DELETE FROM customers;
            DELETE FROM products;
            DELETE FROM product_categories;
            DELETE FROM rooms;
            DELETE FROM room_types;
        ");

        // 2. Insert Room Types (5 types)
        await conn.ExecuteAsync(@"
            INSERT INTO room_types (id, name, hourly_rate, daily_rate, monthly_rate) VALUES (1, 'Standard Single Room', 200, 600, 4500);
            INSERT INTO room_types (id, name, hourly_rate, daily_rate, monthly_rate) VALUES (2, 'Standard Twin Room', 220, 650, 4800);
            INSERT INTO room_types (id, name, hourly_rate, daily_rate, monthly_rate) VALUES (3, 'Deluxe Room', 300, 1000, 7000);
            INSERT INTO room_types (id, name, hourly_rate, daily_rate, monthly_rate) VALUES (4, 'Family Suite', 400, 1500, 11000);
            INSERT INTO room_types (id, name, hourly_rate, daily_rate, monthly_rate) VALUES (5, 'VIP Penthouse', 500, 2500, 18000);
        ");

        // 3. Generate 50 Rooms across 5 floors
        // Floors: 1, 2, 3, 4, 5. Rooms: X01 to X10 on floor X.
        // Status: 0=Available, 1=Occupied, 2=Cleaning, 3=Maintenance, 4=Reserved
        for (int floor = 1; floor <= 5; floor++)
        {
            for (int rNum = 1; rNum <= 10; rNum++)
            {
                int roomId = floor * 100 + rNum;
                string roomNumber = roomId.ToString();
                int typeId = ((rNum - 1) % 5) + 1; // distribute types 1..5
                
                // Distribute statuses:
                // X01: Available (0)
                // X02: Occupied Monthly (1)
                // X03: Occupied Daily (1)
                // X04: Occupied Hourly (1)
                // X05: Reserved (4)
                // X06: Cleaning (2)
                // X07: Maintenance (3)
                // X08: Available (0)
                // X09: Available (0)
                // X10: Available (0)
                int status = 0;
                string notes = "ห้องว่าง";
                if (rNum == 2 || rNum == 3 || rNum == 4) 
                { 
                    status = 1; 
                    notes = rNum switch {
                        2 => "ผู้พักรายเดือนสัญญาต่อเนื่อง",
                        3 => "ผู้พักรายวันเช็คอินแล้ว",
                        _ => "ผู้พักรายชั่วโมง (ชั่วคราว)"
                    };
                }
                else if (rNum == 5) { status = 4; notes = "จองล่วงหน้าผ่านฟรอนท์"; }
                else if (rNum == 6) { status = 2; notes = "รอทำความสะอาดทั่วไป"; }
                else if (rNum == 7) { status = 3; notes = "เครื่องปรับอากาศชำรุด - ส่งซ่อม"; }

                await conn.ExecuteAsync(@"
                    INSERT INTO rooms (id, room_number, room_type_id, floor, status, notes) 
                    VALUES (@Id, @RoomNumber, @TypeId, @Floor, @Status, @Notes)",
                    new { Id = roomId, RoomNumber = roomNumber, TypeId = typeId, Floor = floor.ToString(), Status = status, Notes = notes });
            }
        }

        // 4. Generate 50 Customers
        var firstNames = new[] { 
            "สมชาย", "สมหญิง", "กิตติพงษ์", "มงคล", "วิภาภรณ์", "ปิยะนารถ", "ประเสริฐ", "นงลักษณ์", "ธนพล", "อนันต์",
            "สุรพล", "สมควร", "สุวรรณา", "อารีย์", "วิรุฬห์", "ดนัย", "เกศริน", "พงษ์ศักดิ์", "นัฐพล", "จิราภรณ์",
            "นพดล", "ดาริกา", "ชลสิทธิ์", "ธัญญา", "วีรศักดิ์", "พรเพ็ญ", "ปัญญา", "อุดม", "สมเกียรติ", "วันทนา",
            "บุญส่ง", "สายพิณ", "ชูชาติ", "จรัส", "สุชาติ", "ยุพา", "ประพันธ์", "วรรณีย์", "กมล", "อรุณ",
            "เทียนชัย", "กานดา", "สุรชัย", "สุนทร", "วิรัช", "พัชรี", "ทวี", "นภา", "สมบัติ", "อุบล"
        };
        
        var lastNames = new[] {
            "ดีมาก", "รวยยิ่ง", "สุขใจ", "เด่นดี", "รักสนุก", "เพิ่มพูน", "มั่นคง", "ใจงาม", "รุ่งเรือง", "ดีงาม",
            "สุวรรณ", "เจริญศรี", "วงศ์ษา", "ศรีสุข", "ทองดี", "จิตรดี", "แสงทอง", "มีเจริญ", "ทรัพย์มาก", "วัฒนา",
            "ศิริพงษ์", "เกิดสุข", "กิตติเดช", "ศิริชัย", "มีโชค", "บุญเหลือ", "สุภาพ", "แสงสว่าง", "ยืนยง", "สุขเจริญ",
            "ประเสริฐสุข", "งามขำ", "มงคลศรี", "รักษาสัตย์", "ศิลารักษ์", "บัวหลวง", "พูลสวัสดิ์", "เลิศดี", "เด่นดวง", "สมบูรณ์",
            "พรมดี", "ดวงแก้ว", "แสงแก้ว", "มีลาภ", "ยอดเยี่ยม", "แสนสุข", "พึ่งตนเอง", "เจริญรุ่งเรือง", "รักดี", "ทองแท้"
        };

        for (int i = 1; i <= 50; i++)
        {
            string fullName = $"{firstNames[i - 1]} {lastNames[i - 1]}";
            string phone = $"08{i:D2}-{(i * 13) % 900 + 100:D3}-{(i * 29) % 9000 + 1000:D4}";
            string idCard = $"1100200300{i:D3}";
            string address = i % 2 == 0 ? "กรุงเทพมหานคร" : "นนทบุรี";
            await conn.ExecuteAsync(@"
                INSERT INTO customers (id, full_name, phone, id_card_or_passport, address, created_at, is_deleted) 
                VALUES (@Id, @FullName, @Phone, @IdCard, @Address, datetime('now', '-30 days', 'localtime'), 0)",
                new { Id = i, FullName = fullName, Phone = phone, IdCard = idCard, Address = address });
        }

        // 5. Insert Bookings & Folios (Activities / Stays)
        // Let's create:
        // - 5 active Monthly stays (Rooms 102, 202, 302, 402, 502) with customers 1..5
        // - 5 active Daily stays (Rooms 103, 203, 303, 403, 503) with customers 6..10
        // - 5 active Hourly stays (Rooms 104, 204, 304, 404, 504) with customers 11..15
        // - 5 Advance bookings (Rooms 105, 205, 305, 405, 505) with customers 16..20
        // - 15 Past Checked Out bookings with customers 21..35 (as past history)
        
        // Active Monthly stays: rate_plan=2 (Monthly), agreed_rate=monthly_rate
        for (int floor = 1; floor <= 5; floor++)
        {
            int roomId = floor * 100 + 2;
            int custId = floor; // cust 1..5
            int bookingId = 100 + floor;
            string bookingCode = $"BK-MON-{roomId}";
            decimal monthlyRate = floor switch {
                1 => 4500, 2 => 4800, 3 => 7000, 4 => 11000, _ => 18000
            };

            await conn.ExecuteAsync(@"
                INSERT INTO bookings (id, booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned, check_in_actual, status, agreed_rate, created_at) 
                VALUES (@Id, @Code, @RoomId, @CustId, 2, datetime('now', '-90 days', 'localtime'), datetime('now', '+90 days', 'localtime'), datetime('now', '-90 days', 'localtime'), 1, @Rate, datetime('now', '-90 days', 'localtime'))",
                new { Id = bookingId, Code = bookingCode, RoomId = roomId, CustId = custId, Rate = monthlyRate });

            await conn.ExecuteAsync(@"
                INSERT INTO folios (id, booking_id, is_closed, room_charges, extra_charges, discount_amount, total_amount, created_at) 
                VALUES (@Id, @BookingId, 0, @Rate, 0, 0, @Rate, datetime('now', '-90 days', 'localtime'))",
                new { Id = bookingId, BookingId = bookingId, Rate = monthlyRate });
        }

        // Active Daily stays: rate_plan=1 (Daily), agreed_rate=daily_rate
        for (int floor = 1; floor <= 5; floor++)
        {
            int roomId = floor * 100 + 3;
            int custId = 5 + floor; // cust 6..10
            int bookingId = 200 + floor;
            string bookingCode = $"BK-DAY-{roomId}";
            decimal dailyRate = floor switch {
                1 => 650, 2 => 600, 3 => 1000, 4 => 1500, _ => 2500
            };

            await conn.ExecuteAsync(@"
                INSERT INTO bookings (id, booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned, check_in_actual, status, agreed_rate, created_at) 
                VALUES (@Id, @Code, @RoomId, @CustId, 1, datetime('now', '-2 days', 'localtime'), datetime('now', '+3 days', 'localtime'), datetime('now', '-2 days', 'localtime'), 1, @Rate, datetime('now', '-2 days', 'localtime'))",
                new { Id = bookingId, Code = bookingCode, RoomId = roomId, CustId = custId, Rate = dailyRate });

            decimal totalRoomCharge = dailyRate * 2; // 2 days stayed so far
            await conn.ExecuteAsync(@"
                INSERT INTO folios (id, booking_id, is_closed, room_charges, extra_charges, discount_amount, total_amount, created_at) 
                VALUES (@Id, @BookingId, 0, @Rate, 0, 0, @Rate, datetime('now', '-2 days', 'localtime'))",
                new { Id = bookingId, BookingId = bookingId, Rate = totalRoomCharge });
        }

        // Active Hourly stays: rate_plan=0 (Hourly), agreed_rate=hourly_rate
        for (int floor = 1; floor <= 5; floor++)
        {
            int roomId = floor * 100 + 4;
            int custId = 10 + floor; // cust 11..15
            int bookingId = 300 + floor;
            string bookingCode = $"BK-HR-{roomId}";
            decimal hourlyRate = floor switch {
                1 => 200, 2 => 220, 3 => 300, 4 => 400, _ => 500
            };

            await conn.ExecuteAsync(@"
                INSERT INTO bookings (id, booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned, check_in_actual, status, agreed_rate, created_at) 
                VALUES (@Id, @Code, @RoomId, @CustId, 0, datetime('now', '-2 hours', 'localtime'), datetime('now', '+1 hours', 'localtime'), datetime('now', '-2 hours', 'localtime'), 1, @Rate, datetime('now', '-2 hours', 'localtime'))",
                new { Id = bookingId, Code = bookingCode, RoomId = roomId, CustId = custId, Rate = hourlyRate });

            decimal totalRoomCharge = hourlyRate * 2; // 2 hours stayed so far
            await conn.ExecuteAsync(@"
                INSERT INTO folios (id, booking_id, is_closed, room_charges, extra_charges, discount_amount, total_amount, created_at) 
                VALUES (@Id, @BookingId, 0, @Rate, 0, 0, @Rate, datetime('now', '-2 hours', 'localtime'))",
                new { Id = bookingId, BookingId = bookingId, Rate = totalRoomCharge });
        }

        // Advance bookings: status=0 (Reserved)
        for (int floor = 1; floor <= 5; floor++)
        {
            int roomId = floor * 100 + 5;
            int custId = 15 + floor; // cust 16..20
            int bookingId = 400 + floor;
            string bookingCode = $"BK-RES-{roomId}";
            decimal dailyRate = floor switch {
                1 => 650, 2 => 600, 3 => 1000, 4 => 1500, _ => 2500
            };

            await conn.ExecuteAsync(@"
                INSERT INTO bookings (id, booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned, status, agreed_rate, created_at) 
                VALUES (@Id, @Code, @RoomId, @CustId, 1, datetime('now', '+2 days', 'localtime'), datetime('now', '+5 days', 'localtime'), 0, @Rate, datetime('now', 'localtime'))",
                new { Id = bookingId, Code = bookingCode, RoomId = roomId, CustId = custId, Rate = dailyRate });
        }

        // Past Checked Out bookings: status=2 (CheckedOut), is_closed=1 (Folio closed)
        for (int i = 1; i <= 15; i++)
        {
            int floor = (i % 5) + 1;
            int roomId = floor * 100 + 8; // Room X08 (Available now)
            int custId = 20 + i; // cust 21..35
            int bookingId = 500 + i;
            string bookingCode = $"BK-PAST-{roomId}-{i}";
            decimal dailyRate = floor switch {
                1 => 600, 2 => 650, 3 => 1000, 4 => 1500, _ => 2500
            };
            decimal totalRoomCharge = dailyRate * 3; // 3 days stay

            await conn.ExecuteAsync(@"
                INSERT INTO bookings (id, booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned, check_in_actual, check_out_actual, status, agreed_rate, created_at) 
                VALUES (@Id, @Code, @RoomId, @CustId, 1, datetime('now', '-10 days', 'localtime'), datetime('now', '-7 days', 'localtime'), datetime('now', '-10 days', 'localtime'), datetime('now', '-7 days', 'localtime'), 2, @Rate, datetime('now', '-10 days', 'localtime'))",
                new { Id = bookingId, Code = bookingCode, RoomId = roomId, CustId = custId, Rate = dailyRate });

            await conn.ExecuteAsync(@"
                INSERT INTO folios (id, booking_id, is_closed, room_charges, extra_charges, discount_amount, total_amount, created_at, closed_at) 
                VALUES (@Id, @BookingId, 1, @Rate, 0, 0, @Rate, datetime('now', '-10 days', 'localtime'), datetime('now', '-7 days', 'localtime'))",
                new { Id = bookingId, BookingId = bookingId, Rate = totalRoomCharge });
        }

        // 6. Products Categories & Products
        await conn.ExecuteAsync(@"
            INSERT INTO product_categories (id, name, is_active) VALUES (1, 'เครื่องดื่ม', 1);
            INSERT INTO product_categories (id, name, is_active) VALUES (2, 'ขนมขบเคี้ยว', 1);
            INSERT INTO product_categories (id, name, is_active) VALUES (3, 'ของใช้ในห้องน้ำ', 1);

            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (1, 1, 'เป๊ปซี่', 20.00, 10.00, 75, 1, 1);
            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (2, 1, 'น้ำเปล่า', 10.00, 4.00, 180, 1, 1);
            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (3, 1, 'เบียร์ช้าง', 50.00, 30.00, 20, 1, 1);
            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (4, 2, 'มันฝรั่งเลย์', 30.00, 15.00, 45, 1, 1);
            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (5, 3, 'แปรงสีฟัน', 15.00, 7.00, 50, 1, 1);
            INSERT INTO products (id, category_id, name, price, cost, stock_qty, track_stock, is_active) VALUES (6, 3, 'สบู่เหลว', 45.00, 20.00, 30, 1, 1);
        ");

        // 7. POS Sales, Items & Payments (Multiple cases)
        // Let's create about 15 POS sales transactions:
        // - 5 billed to active daily stays folios
        // - 5 retail cash sales (today)
        // - 5 retail promptpay sales (past days)
        for (int i = 1; i <= 15; i++)
        {
            int saleId = 100 + i;
            string saleCode = $"SL-{100000 + i}";
            int custId = 30 + i; // customer 31..45
            decimal amount = i * 20.00m;
            int? folioId = i <= 5 ? (200 + i) : null; // first 5 billed to active daily stays folios (201..205)

            await conn.ExecuteAsync(@"
                INSERT INTO sales (id, sale_code, folio_id, customer_id, sub_total, discount_amount, tax_amount, total_amount, created_at, is_deleted) 
                VALUES (@Id, @Code, @FolioId, @CustId, @Amount, 0, 0, @Amount, datetime('now', @DaysDiff, 'localtime'), 0)",
                new { Id = saleId, Code = saleCode, FolioId = folioId, CustId = custId, Amount = amount, DaysDiff = $"-{i % 4} days" });

            // items
            await conn.ExecuteAsync(@"
                INSERT INTO sale_items (sale_id, product_id, product_name_snapshot, unit_price, quantity, line_total) 
                VALUES (@SaleId, 1, 'เป๊ปซี่', 20.00, @Qty, @LineTotal)",
                new { SaleId = saleId, Qty = i % 3 + 1, LineTotal = (i % 3 + 1) * 20.00m });

            if (folioId.HasValue)
            {
                // Update folio extra charges
                await conn.ExecuteAsync(@"
                    UPDATE folios SET extra_charges = extra_charges + @Amount, total_amount = total_amount + @Amount WHERE id = @FolioId",
                    new { Amount = amount, FolioId = folioId.Value });
            }
            else
            {
                // cash or promptpay payment
                int method = i % 2 == 0 ? 0 : 1; // 0=Cash, 1=PromptPay
                await conn.ExecuteAsync(@"
                    INSERT INTO payments (id, sale_id, method, amount, reference_no, paid_at) 
                    VALUES (@Id, @SaleId, @Method, @Amount, @Ref, datetime('now', @DaysDiff, 'localtime'))",
                    new { Id = 200 + i, SaleId = saleId, Method = method, Amount = amount, Ref = $"TX-{saleCode}", DaysDiff = $"-{i % 4} days" });
            }
        }

        // 8. Utility Bills over 4 months for all monthly stays (102, 202, 302, 402, 502)
        var now = DateTime.Now;
        var month0 = now.ToString("yyyy-MM");
        var month1 = now.AddMonths(-1).ToString("yyyy-MM");
        var month2 = now.AddMonths(-2).ToString("yyyy-MM");
        var month3 = now.AddMonths(-3).ToString("yyyy-MM");

        for (int floor = 1; floor <= 5; floor++)
        {
            int roomId = floor * 100 + 2;
            int roomTypeBillId = 1000 + floor;
            decimal roomCharge = floor switch {
                1 => 4500, 2 => 4800, 3 => 7000, 4 => 11000, _ => 18000
            };

            // Month -3: Bill (Paid)
            await conn.ExecuteAsync($@"
                INSERT INTO utility_bills (id, bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, water_prev, water_curr, water_units, water_rate, water_amount, common_area_fee, garbage_fee, total_amount, is_paid, paid_at, created_at) 
                VALUES (@Id3, @Code3, @RoomId, '{month3}', @RoomCharge, 50, 100, 50, 8.00, 400.00, 5, 10, 5, 18.00, 90.00, 0, 20.00, @Total3, 1, datetime('now', '-85 days', 'localtime'), datetime('now', '-85 days', 'localtime'))",
                new { Id3 = roomTypeBillId * 10 + 4, Code3 = $"UB-{roomId}-00", RoomId = roomId, RoomCharge = roomCharge, Total3 = roomCharge + 510.00m });

            // Meter Readings Month -3
            await conn.ExecuteAsync($@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount, recorded_at)
                VALUES (@RoomId, 0, '{month3}', 50, 100, 50, 8.00, 400.00, datetime('now', '-85 days', 'localtime')),
                       (@RoomId, 1, '{month3}', 5, 10, 5, 18.00, 90.00, datetime('now', '-85 days', 'localtime'));",
                new { RoomId = roomId });

            // Month -2: Bill (Paid)
            await conn.ExecuteAsync($@"
                INSERT INTO utility_bills (id, bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, water_prev, water_curr, water_units, water_rate, water_amount, common_area_fee, garbage_fee, total_amount, is_paid, paid_at, created_at) 
                VALUES (@Id2, @Code2, @RoomId, '{month2}', @RoomCharge, 100, 150, 50, 8.00, 400.00, 10, 15, 5, 18.00, 90.00, 0, 20.00, @Total2, 1, datetime('now', '-55 days', 'localtime'), datetime('now', '-55 days', 'localtime'))",
                new { Id2 = roomTypeBillId * 10 + 1, Code2 = $"UB-{roomId}-01", RoomId = roomId, RoomCharge = roomCharge, Total2 = roomCharge + 510.00m });

            // Meter Readings Month -2
            await conn.ExecuteAsync($@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount, recorded_at)
                VALUES (@RoomId, 0, '{month2}', 100, 150, 50, 8.00, 400.00, datetime('now', '-55 days', 'localtime')),
                       (@RoomId, 1, '{month2}', 10, 15, 5, 18.00, 90.00, datetime('now', '-55 days', 'localtime'));",
                new { RoomId = roomId });

            // Month -1: Bill (Overdue for Room 102 & 302, Paid for others)
            int isPaidMonth1 = (floor == 1 || floor == 3) ? 0 : 1;
            string createdDiffMonth1 = (floor == 1 || floor == 3) ? "-40 days" : "-25 days";
            await conn.ExecuteAsync($@"
                INSERT INTO utility_bills (id, bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, water_prev, water_curr, water_units, water_rate, water_amount, common_area_fee, garbage_fee, total_amount, is_paid, paid_at, created_at) 
                VALUES (@Id1, @Code1, @RoomId, '{month1}', @RoomCharge, 150, 210, 60, 8.00, 480.00, 15, 21, 6, 18.00, 108.00, 0, 20.00, @Total1, @IsPaid1, @PaidAt1, datetime('now', '{createdDiffMonth1}', 'localtime'))",
                new { 
                    Id1 = roomTypeBillId * 10 + 2, 
                    Code1 = $"UB-{roomId}-02", 
                    RoomId = roomId, 
                    RoomCharge = roomCharge, 
                    Total1 = roomCharge + 608.00m,
                    IsPaid1 = isPaidMonth1,
                    PaidAt1 = isPaidMonth1 == 1 ? (object)now.AddDays(-25).ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value
                });

            // Meter Readings Month -1
            await conn.ExecuteAsync($@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount, recorded_at)
                VALUES (@RoomId, 0, '{month1}', 150, 210, 60, 8.00, 480.00, datetime('now', '-25 days', 'localtime')),
                       (@RoomId, 1, '{month1}', 15, 21, 6, 18.00, 108.00, datetime('now', '-25 days', 'localtime'));",
                new { RoomId = roomId });

            // Month 0: Bill
            // Room 102: Overdue (created 25 days ago)
            // Room 202: Due Soon (created 2 days ago)
            // Room 302: Overdue (created 20 days ago)
            // Room 402: Due Soon (created 1 day ago)
            // Room 502: Paid (created 5 days ago)
            int isPaidMonth0 = floor == 5 ? 1 : 0;
            string createdDiffMonth0 = floor switch {
                1 => "-25 days",
                2 => "-2 days",
                3 => "-20 days",
                4 => "-1 days",
                _ => "-5 days"
            };

            await conn.ExecuteAsync($@"
                INSERT INTO utility_bills (id, bill_code, room_id, billing_month, room_charge, electric_prev, electric_curr, electric_units, electric_rate, electric_amount, water_prev, water_curr, water_units, water_rate, water_amount, common_area_fee, garbage_fee, total_amount, is_paid, paid_at, created_at) 
                VALUES (@Id0, @Code0, @RoomId, '{month0}', @RoomCharge, 210, 290, 80, 8.00, 640.00, 21, 29, 8, 18.00, 144.00, 0, 20.00, @Total0, @IsPaid0, @PaidAt0, datetime('now', '{createdDiffMonth0}', 'localtime'))",
                new { 
                    Id0 = roomTypeBillId * 10 + 3, 
                    Code0 = $"UB-{roomId}-03", 
                    RoomId = roomId, 
                    RoomCharge = roomCharge, 
                    Total0 = roomCharge + 804.00m,
                    IsPaid0 = isPaidMonth0,
                    PaidAt0 = isPaidMonth0 == 1 ? (object)now.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value
                });

            // Meter Readings Month 0
            await conn.ExecuteAsync($@"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount, recorded_at)
                VALUES (@RoomId, 0, '{month0}', 210, 290, 80, 8.00, 640.00, datetime('now', '{createdDiffMonth0}', 'localtime')),
                       (@RoomId, 1, '{month0}', 21, 29, 8, 18.00, 144.00, datetime('now', '{createdDiffMonth0}', 'localtime'));",
                new { RoomId = roomId });
        }

        // 9. Add Audit Trail logs corresponding to these events
        await conn.ExecuteAsync(@"
            INSERT INTO audit_logs (action, entity_name, entity_id, detail_json, created_at) 
            VALUES ('SEED_MOCK', 'System', '0', 'จำลองข้อมูลระบบแบบครอบคลุม: ผู้เช่ารายเดือนอยู่มาหลายเดือน (4 เดือนย้อนหลัง), มีสถานะใกล้ครบกำหนด และเลยกำหนดชำระ', datetime('now', 'localtime'));
        ");
    }
}
