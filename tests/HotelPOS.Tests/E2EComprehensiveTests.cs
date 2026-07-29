using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Licensing;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

[Collection("Licensing Tests Collection")]
public class E2EComprehensiveTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly string _tempLicenseFolder;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    // Repositories
    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IFolioRepository _folioRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IMeterReadingRepository _meterReadingRepository;
    private readonly IUtilityBillRepository _utilityBillRepository;
    private readonly IAuditRepository _auditRepository;

    // Services
    private readonly IRoomService _roomService;
    private readonly ICustomerService _customerService;
    private readonly IBookingService _bookingService;
    private readonly ISettingsService _settingsService;
    private readonly IUtilityBillService _utilityBillService;
    private readonly IAuditService _auditService;
    private readonly IBackupService _backupService;
    private readonly IExportImportService _exportImportService;

    // Simulate Private Key for License Signing in Tests
    private const string TestPrivateKeyBase64 = "MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDHGAL/OQhKcQQC1jvBIrntmAX0Sbg/qtYkRm6QN0uvYOX4Mthlu8ADQK6KZSVBYxCXaCA6nho6bTOGpCJmnDakj1BtOs6n3D/LvPKj7MMZ3sCEqvktWiJlKFNPHKtZbMpfXI+bqrxSkCxBDbFmrnG/PaU94rR+bXAluzXbzhcCH6gEmtKTUx6VM+EI/PVIlCdZMjcrkTO7aP7UCMFEnTkvuWMpuuHp1NmWUTEwNvqH9BnkkIdlPIhHpqPdegu93YraD71F5WIG8SU3rSO/wvPgHQTM7HCd8xRbchULLktPrEORHN6JC1ZJBkr1RbacgkHIpljJaxep0Yj/+NHowyl1AgMBAAECggEBAIIY7bRrR0ClszJLXcap84cPZSypk41/C+muYIc6qulST1QtnXx1AFbfyG5FA+BDZM8bSpwjPg5Z12avEI+umoJT6AFIgUvtP37Z3FBD4YWhKnpG4wbAtGMXw8CZglqwHVnNOUZGfkMRVOm5kegAK/IEzVqwLrPCvZraR6p3dE98yseuQdKwy/KNuA0PbCOA8Md8Le+hng36DAAdcn8kHKksi9W8gBqS9qB5LKnla4kXNKeYPGDBKhjaCf45k2aJtnBHMd74/P1y+VkeJMlSjH8elx9rDbzkn+CvmSBY/BDLLlpuD2nftPSuZ8yWNp/krG5lufUdFsFa8kHoqJnH+W0CgYEA15L4D2ZFC7stFenwPLGjbh0SFtQZACzM48xMX3I2Ecuro+qONrdHgZ7Q0wm6b1W1dUkUeNSZ4wMiux/lhhaHYbBqMbjRpIagPGsN6+62KPOsK+L90OqPz5N49BYdF0NuBTQSif1xGP39cv7LX2JwUEYaoSs7lTYVGJ73yQMnU5MCgYEA7G3gIjYt7PTJBWNtVt1dUJ1TNdIz6B6UM/sMWw0t3qCMR4oQBJz7E8NmZLIUeS0TT7McDaaCymnn2/JKBdXWWu8dM8KGjm9tzq6CPPd5Lvt2aUWkijFfwtVg6SYSmwp786SfStsNjXKED7xiqU03GwT8nLf8TewgCB7lV6uBw9cCgYEAlVEVRQVfedqyReV+I2wfeVvlda5/iqF9YaPWmp3vWbArOSR0UO3uN5gbqLGqUweY4p418ePAm39GhTp4rsHYEBAz3jDX9Q/S2UaFpA/6WK8/aD6X9CckaXEKbHcMu1pXUH9a//1uYxM6hHZ7w5vZk6CbPVtGr/l/70fc9XybtsUCgYAtaJDyoStC5mSxZz45v7xLXlv760pS24SlUyM1XZugtX8bwlV/PVMvoYjJ8DXkbBbYaNMLgB6Al8STRr6WzlIkFuap6UOEmbwiRPv4j6MztdIxN9H5RLBasDazsL9EDchurAB4FQhOUV8x0oG0eIML6nJF+0Q3BxHD3YM4ylTa8wKBgB+UjpKeSIcMuG1Em5wbXbLbzNwxjuPX6TwyZKHmzOZZRfZq/4ppJaV66h8pngQC1ZZOBMDI/IWIKorM40hFqHGXmnp+Z7dFwsjoRWoC77/Y6plkb4qq/Od5ZnCVLbBN8uTK3hYdAUd2OfYQ/m6E5CNkSA/xUGGgm8Zhzlf58ezI";

    public E2EComprehensiveTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-e2etest-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-e2etest-logs-{Guid.NewGuid():N}");
        _tempLicenseFolder = Path.Combine(Path.GetTempPath(), $"hotelpos-e2etest-lic-{Guid.NewGuid():N}");

        if (!Directory.Exists(_tempLicenseFolder))
        {
            Directory.CreateDirectory(_tempLicenseFolder);
        }

        // Redirect Licensing Registry paths for testing isolation
        TrialManager.RegistrySubKey = @"Software\PSoftRestRentManager\E2ETests";
        TrialManager.RegistryValueName = "E2ETestTData";
        TrialManager.HiddenFileName = ".e2e-test-tdata";

        LicenseManager.LicenseRegistryValueName = "E2ETestLData";
        LicenseManager.LicenseFileName = "e2e-test-license.dat";

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        // Run migrations
        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        // Repositories Initialization
        _roomRepository = new RoomRepository(_connectionFactory, _logger);
        _bookingRepository = new BookingRepository(_connectionFactory, _logger);
        _customerRepository = new CustomerRepository(_connectionFactory, _logger);
        _folioRepository = new FolioRepository(_connectionFactory, _logger);
        _settingsRepository = new SettingsRepository(_connectionFactory, _logger);
        _meterReadingRepository = new MeterReadingRepository(_connectionFactory, _logger);
        _utilityBillRepository = new UtilityBillRepository(_connectionFactory, _logger);
        _auditRepository = new AuditRepository(_connectionFactory, _logger);

        // Services Initialization
        _settingsService = new SettingsService(_settingsRepository, _logger);
        _roomService = new RoomService(_roomRepository, _logger);
        _customerService = new CustomerService(_customerRepository, _logger);
        _bookingService = new BookingService(_bookingRepository, _roomRepository, _customerRepository, _folioRepository, _logger);
        _utilityBillService = new UtilityBillService(_meterReadingRepository, _utilityBillRepository, _settingsService, _roomRepository, _logger);
        _auditService = new AuditService(_auditRepository, _logger);
        _backupService = new BackupService(_connectionFactory, _auditService, _logger);
        _exportImportService = new ExportImportService(_customerService, _roomService, _auditService);
    }

    [Fact]
    public async Task Run_Complete_HotelPOS_E2E_Workflow()
    {
        // ==========================================
        // STAGE 1: Database Setup & Seed Verification
        // ==========================================
        using (var connection = _connectionFactory.CreateConnection())
        {
            var rolesCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM roles");
            Assert.True(rolesCount >= 2, "Roles seed data should have at least 2 roles (Admin & Staff)");

            var usersCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
            Assert.True(usersCount >= 1, "Users seed data should have at least 1 user (admin)");

            var adminUser = await connection.QuerySingleOrDefaultAsync<User>(@"
                SELECT id AS Id, username AS Username, password_hash AS PasswordHash, 
                       full_name AS FullName, role_id AS RoleId, is_active AS IsActive, 
                       last_login_at AS LastLoginAt, created_at AS CreatedAt 
                FROM users 
                WHERE username = 'admin'");
            Assert.NotNull(adminUser);
            Assert.Equal("ผู้ดูแลระบบ", adminUser.FullName);
        }

        // ==========================================
        // STAGE 2: Settings Management
        // ==========================================
        var initialShopName = await _settingsService.GetShopNameAsync();
        Assert.Equal("ชื่อร้าน/โรงแรมของคุณ", initialShopName);

        await _settingsService.SetAsync("shop_name", "โรงแรม แสนสุข E2E");
        var updatedShopName = await _settingsService.GetShopNameAsync();
        Assert.Equal("โรงแรม แสนสุข E2E", updatedShopName);

        await _settingsService.SetAsync("receipt_doc_prefix", "BILL");
        var nextDocNum = await _settingsService.GetNextDocumentNumberAsync();
        Assert.Contains("BILL", nextDocNum); // E.g., BILL-000001 or similar depending on settings format

        // ==========================================
        // STAGE 3: Licensing Integrity Audit
        // ==========================================
        // 3.1 Verify Hardware ID generation works and is robust
        var hwId = HardwareIdGenerator.Generate();
        Assert.NotNull(hwId);
        Assert.Equal(64, hwId.Length);

        // 3.2 Verify Trial mode behavior
        ClearTestRegistryAndFiles(deleteDb: false);
        using (var connection = _connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync("DELETE FROM settings WHERE key = 'trial_start_date'");
        }
        var (isTrialActive, trialDaysRemaining) = TrialManager.GetTrialStatus(_tempDbPath, _tempLicenseFolder);
        Assert.True(isTrialActive);
        Assert.Equal(30, trialDaysRemaining);

        // 3.3 Test License Validation with a valid Signed USB Dongle File
        var usbHwId = UsbDongleManager.HashUsbSerial("E2E-TEST-USB-DONGLE-SERIAL");
        var appSerial = "E2E-APP-SERIAL-1.0.0";
        var license = new LicenseFile
        {
            CustomerName = "ลูกค้า E2E Test Room",
            UsbHardwareId = usbHwId,
            AppSerial = appSerial,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(365),
            MaxRooms = 15,
            Features = new List<string> { "BOOKING", "POS", "UTILITIES" }
        };
        SignLicense(license);

        var dongleResult = LicenseValidator.ValidateDongle(license, usbHwId, appSerial);
        Assert.Equal(LicenseStatus.Active, dongleResult);

        // 3.4 Test modification detection
        license.MaxRooms = 99; // Alter content without re-signing
        var corruptedResult = LicenseValidator.ValidateDongle(license, usbHwId, appSerial);
        Assert.Equal(LicenseStatus.Invalid, corruptedResult);

        // 3.5 Test Revocation verification
        var revokedFile = new RevocationListFile
        {
            IssuedAt = DateTime.Now,
            RevokedHardwareIds = new List<string> { hwId }
        };
        SignRevocation(revokedFile);
        File.WriteAllText(Path.Combine(_tempLicenseFolder, RevocationManager.RevocationFileName), revokedFile.ToJson());

        var licenseToRevoke = new LicenseFile
        {
            CustomerName = "ลูกค้า โดนระงับสิทธิ์",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(30)
        };
        SignLicense(licenseToRevoke);

        var verifyRevokedResult = LicenseValidator.Validate(licenseToRevoke, hwId, null, _tempLicenseFolder);
        Assert.Equal(LicenseStatus.Revoked, verifyRevokedResult);

        // ==========================================
        // STAGE 4: Room & Type Administration
        // ==========================================
        var deluxeTypeId = await _roomService.SaveRoomTypeAsync(new RoomType
        {
            Name = "Deluxe Room",
            DailyRate = 1500m,
            HourlyRate = 350m,
            MonthlyRate = 18000m,
            Description = "วิวทะเลแอร์เย็นเจี๊ยบ"
        });
        Assert.True(deluxeTypeId > 0);

        var standardTypeId = await _roomService.SaveRoomTypeAsync(new RoomType
        {
            Name = "Standard Room",
            DailyRate = 800m,
            HourlyRate = 200m,
            MonthlyRate = 8500m,
            Description = "ห้องพัดลมราคาประหยัด"
        });
        Assert.True(standardTypeId > 0);

        // Add rooms
        var room101Id = await _roomService.SaveRoomAsync(new Room
        {
            RoomNumber = "101",
            Floor = "1",
            RoomTypeId = deluxeTypeId,
            Status = RoomStatus.Available
        });
        Assert.True(room101Id > 0);

        var room102Id = await _roomService.SaveRoomAsync(new Room
        {
            RoomNumber = "102",
            Floor = "1",
            RoomTypeId = standardTypeId,
            Status = RoomStatus.Available
        });
        Assert.True(room102Id > 0);

        // Test constraint: Prevent duplicate room numbers
        var duplicateRoom = new Room
        {
            RoomNumber = "101", // Duplicate
            Floor = "2",
            RoomTypeId = standardTypeId
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _roomService.SaveRoomAsync(duplicateRoom));

        // ==========================================
        // STAGE 5: Customer Management & Import/Export
        // ==========================================
        var customerId = await _customerService.SaveCustomerAsync(new Customer
        {
            FullName = "นายรักสะอาด มาหาดใหญ่",
            Phone = "0888888888",
            Email = "rak.saad@gmail.com",
            IdCardOrPassport = "1234567890123",
            Address = "123 ตรัง ซิตี้"
        });
        Assert.True(customerId > 0);

        // CSV Export/Import check
        var tempCsvCustomersPath = Path.Combine(Path.GetTempPath(), $"customers-{Guid.NewGuid():N}.csv");
        var tempCsvRoomsPath = Path.Combine(Path.GetTempPath(), $"rooms-{Guid.NewGuid():N}.csv");

        try
        {
            // Export
            await _exportImportService.ExportCustomersToCsvAsync(tempCsvCustomersPath);
            await _exportImportService.ExportRoomsToCsvAsync(tempCsvRoomsPath);

            Assert.True(File.Exists(tempCsvCustomersPath));
            Assert.True(File.Exists(tempCsvRoomsPath));

            // Clean customer to test import
            await _customerService.DeleteCustomerAsync(customerId);
            var deletedCustomer = await _customerService.GetCustomerByIdAsync(customerId);
            Assert.Null(deletedCustomer);

            // Import
            var importedCustomersCount = await _exportImportService.ImportCustomersFromCsvAsync(tempCsvCustomersPath);
            Assert.True(importedCustomersCount > 0);

            var customersList = await _customerService.GetCustomersAsync("รักสะอาด");
            Assert.NotEmpty(customersList);
        }
        finally
        {
            if (File.Exists(tempCsvCustomersPath)) File.Delete(tempCsvCustomersPath);
            if (File.Exists(tempCsvRoomsPath)) File.Delete(tempCsvRoomsPath);
        }

        // Re-fetch customer for bookings
        var customers = await _customerService.GetCustomersAsync("รักสะอาด");
        var testCustomer = customers.First();

        // ==========================================
        // STAGE 6: Booking & Room Status Workflow
        // ==========================================
        // 6.1 Reservation Flow
        var checkInPlanned = DateTime.Now.AddDays(1);
        var checkOutPlanned = DateTime.Now.AddDays(4);

        var reservation = await _bookingService.CreateReservationAsync(
            room101Id,
            testCustomer,
            RatePlanType.Daily,
            1500m,
            checkInPlanned,
            checkOutPlanned,
            "จองสัมมนาล่วงหน้า"
        );

        Assert.NotNull(reservation);
        Assert.Equal(BookingStatus.Reserved, reservation.Status);

        var roomStatusReserved = await _roomService.GetRoomByIdAsync(room101Id);
        Assert.Equal(RoomStatus.Reserved, roomStatusReserved!.Status);

        // 6.2 Check-in reservation
        await _bookingService.CheckInExistingBookingAsync(reservation.Id);
        var activeBooking = await _bookingService.GetBookingByIdAsync(reservation.Id);
        Assert.NotNull(activeBooking);
        Assert.Equal(BookingStatus.CheckedIn, activeBooking!.Status);
        Assert.NotNull(activeBooking.CheckInActual);

        var roomStatusOccupied = await _roomService.GetRoomByIdAsync(room101Id);
        Assert.Equal(RoomStatus.Occupied, roomStatusOccupied!.Status);

        // ==========================================
        // STAGE 7: Utility Meter Reading & Billing
        // ==========================================
        // Set rates in settings first
        await _settingsService.SetAsync("electric_rate_per_unit", "9.00");
        await _settingsService.SetAsync("water_rate_per_unit", "20.00");
        await _settingsService.SetAsync("water_billing_mode", "METER");
        await _settingsService.SetAsync("common_area_fee", "150.00");
        await _settingsService.SetAsync("garbage_fee", "50.00");

        // 7.1 Record electricity meters
        int readingElecId = await _utilityBillService.RecordMeterReadingAsync(
            room102Id, UtilityType.Electric, 1500m, 1680m, "2026-07");
        Assert.True(readingElecId > 0);

        // 7.2 Record water meters in METER mode
        int readingWaterId = await _utilityBillService.RecordMeterReadingAsync(
            room102Id, UtilityType.Water, 450m, 475m, "2026-07");
        Assert.True(readingWaterId > 0);

        // Generate bill
        var utilityBill = await _utilityBillService.GenerateMonthlyBillAsync(room102Id, "2026-07");
        Assert.NotNull(utilityBill);
        Assert.StartsWith("UB-", utilityBill.BillCode);
        Assert.False(utilityBill.IsPaid);
        Assert.Equal(8500m, utilityBill.RoomCharge); // Standard room monthly rate is 8500

        // Electricity: (1680 - 1500) = 180 units * 9.00 = 1620
        Assert.Equal(1620m, utilityBill.ElectricAmount);
        // Water: (475 - 450) = 25 units * 20.00 = 500
        Assert.Equal(500m, utilityBill.WaterAmount);

        // Total: 8500 (Room) + 1620 (Elec) + 500 (Water) + 150 (Common) + 50 (Garbage) = 10820
        Assert.Equal(10820m, utilityBill.TotalAmount);

        // Pay utility bill
        await _utilityBillService.MarkBillAsPaidAsync(utilityBill.Id, PaymentMethod.PromptPay);
        var paidBill = await _utilityBillRepository.GetByIdAsync(utilityBill.Id);
        Assert.NotNull(paidBill);
        Assert.True(paidBill!.IsPaid);
        Assert.Equal(PaymentMethod.PromptPay, paidBill.PaymentMethod);

        // 7.3 Test FLAT mode for Water (เหมาจ่าย)
        await _settingsService.SetAsync("water_billing_mode", "FLAT");
        await _settingsService.SetAsync("water_flat_rate_per_person", "120.00");

        // We delete the previous bill to generate a new one under FLAT mode (due to unique key room_id + billing_month)
        using (var connection = _connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync("DELETE FROM utility_bills WHERE id = @Id", new { Id = utilityBill.Id });
        }

        var flatUtilityBill = await _utilityBillService.GenerateMonthlyBillAsync(room102Id, "2026-07", waterPersonCount: 3);
        Assert.Equal("FLAT", flatUtilityBill.WaterBillingMode);
        Assert.Equal(3, flatUtilityBill.WaterPersonCount);
        // Water: 3 * 120 = 360
        Assert.Equal(360m, flatUtilityBill.WaterAmount);

        // ==========================================
        // STAGE 8: POS / Sales Table Integration Simulation
        // ==========================================
        // Since POS services aren't active in core, E2E verifies direct database read/write schema constraint rules
        using (var connection = _connectionFactory.CreateConnection())
        {
            // Insert Product Category
            var categoryId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO product_categories (name, is_active)
                VALUES ('มินิบาร์', 1) RETURNING id;");
            Assert.True(categoryId > 0);

            // Insert Product
            var productId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO products (category_id, name, sku, price, cost, stock_qty, track_stock, is_active)
                VALUES (@CatId, 'มันฝรั่งทอด', 'SKU-001', 40.00, 25.00, 10, 1, 1) RETURNING id;",
                new { CatId = categoryId });
            Assert.True(productId > 0);

            // Insert Sale
            var saleId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO sales (sale_code, folio_id, customer_id, sub_total, discount_amount, tax_amount, total_amount, created_by)
                VALUES ('SALE-0001', NULL, @CustId, 80.00, 10.00, 0, 70.00, 1) RETURNING id;",
                new { CustId = testCustomer.Id });
            Assert.True(saleId > 0);

            // Insert Sale Item
            var saleItemId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO sale_items (sale_id, product_id, product_name_snapshot, unit_price, quantity, line_total)
                VALUES (@SaleId, @ProdId, 'มันฝรั่งทอด', 40.00, 2, 80.00) RETURNING id;",
                new { SaleId = saleId, ProdId = productId });
            Assert.True(saleItemId > 0);

            // Insert Payment
            var paymentId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO payments (sale_id, method, amount, reference_no, received_by)
                VALUES (@SaleId, 1, 70.00, 'TXN-E2E-POS', 1) RETURNING id;",
                new { SaleId = saleId });
            Assert.True(paymentId > 0);

            // Insert Invoice Document
            var docId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO invoice_documents (sale_id, doc_type, document_number, printed_paper_size, printed_by, print_count)
                VALUES (@SaleId, 1, 'RC-2026-0001', 1, 1, 1) RETURNING id;",
                new { SaleId = saleId });
            Assert.True(docId > 0);

            // Assert relational consistency
            var fullSale = await connection.QuerySingleAsync<Sale>(@"
                SELECT id AS Id, sale_code AS SaleCode, folio_id AS FolioId, 
                       customer_id AS CustomerId, sub_total AS SubTotal, 
                       discount_amount AS DiscountAmount, tax_amount AS TaxAmount, 
                       total_amount AS TotalAmount, created_by AS CreatedBy, 
                       created_at AS CreatedAt, is_deleted AS IsDeleted 
                FROM sales 
                WHERE id = @Id", new { Id = saleId });
            Assert.Equal(70.00m, fullSale.TotalAmount);

            var item = await connection.QuerySingleAsync<SaleItem>(@"
                SELECT id AS Id, sale_id AS SaleId, product_id AS ProductId, 
                       product_name_snapshot AS ProductNameSnapshot, 
                       unit_price AS UnitPrice, quantity AS Quantity, 
                       line_total AS LineTotal 
                FROM sale_items 
                WHERE sale_id = @Id", new { Id = saleId });
            Assert.Equal("มันฝรั่งทอด", item.ProductNameSnapshot);
            Assert.Equal(2, item.Quantity);

            // ==========================================
            // STAGE 8.5: POS Void Sale and Stock Restoration Test
            // ==========================================
            var productRepository = new ProductRepository(_connectionFactory, _logger);
            var saleRepository = new SaleRepository(_connectionFactory, _logger);
            var posService = new POSService(productRepository, saleRepository, _connectionFactory, _logger);

            // Verify initial stock of product (which is 10)
            var prodBefore = await productRepository.GetProductByIdAsync(productId);
            Assert.Equal(10, prodBefore!.StockQty);

            // Void the sale
            await posService.VoidSaleAsync(saleId);

            // Verify that the sale is marked as deleted
            var saleAfterVoid = await saleRepository.GetSaleByIdAsync(saleId);
            Assert.Null(saleAfterVoid); // GetSaleByIdAsync filters out is_deleted = 1

            // Let's query directly to check is_deleted flag
            var rawIsDeleted = await connection.ExecuteScalarAsync<int>("SELECT is_deleted FROM sales WHERE id = @Id", new { Id = saleId });
            Assert.Equal(1, rawIsDeleted);

            // Verify stock is restored (+2 because the quantity sold was 2)
            var prodAfter = await productRepository.GetProductByIdAsync(productId);
            Assert.Equal(12, prodAfter!.StockQty);
        }

        // ==========================================
        // STAGE 9: Check-out & Folio Settlement
        // ==========================================
        // Add extra charges to booking folio during stay
        var guestFolioBefore = await _bookingService.GetFolioByBookingIdAsync(activeBooking.Id);
        Assert.NotNull(guestFolioBefore);

        // Perform checkout with extra charges 450 (e.g. laundry/room services) and 100 discount
        var finalizedFolio = await _bookingService.CheckOutAsync(activeBooking.Id, extraCharges: 450m, discountAmount: 100m, notes: "เรียบร้อยดี ห้องสะอาด");
        Assert.True(finalizedFolio.IsClosed);
        Assert.NotNull(finalizedFolio.ClosedAt);

        // Calculations check: 1 night * 1500/night = 1500 (Room charges)
        Assert.Equal(1500m, finalizedFolio.RoomCharges);
        Assert.Equal(450m, finalizedFolio.ExtraCharges);
        Assert.Equal(100m, finalizedFolio.DiscountAmount);
        // Total: 1500 + 450 - 100 = 1850
        Assert.Equal(1850m, finalizedFolio.TotalAmount);

        // Verify room transitions to cleaning
        var roomClean = await _roomService.GetRoomByIdAsync(room101Id);
        Assert.Equal(RoomStatus.Cleaning, roomClean!.Status);

        // Make room available again
        await _roomService.UpdateRoomStatusAsync(room101Id, RoomStatus.Available, "ทำความสะอาดเรียบร้อย");
        var roomCleaned = await _roomService.GetRoomByIdAsync(room101Id);
        Assert.Equal(RoomStatus.Available, roomCleaned!.Status);

        // ==========================================
        // STAGE 10: Backup, Restore, & Reset Lifecycle
        // ==========================================
        var backupPath = Path.Combine(Path.GetTempPath(), $"hotelpos-backup-{Guid.NewGuid():N}.db");
        try
        {
            // 10.1 Create Backup
            await _backupService.CreateBackupAsync(backupPath);
            Assert.True(File.Exists(backupPath));

            // Verify integrity of database settings and check room count
            var roomsBefore = await _roomService.GetRoomsAsync();
            Assert.Equal(2, roomsBefore.Count());

            // 10.2 Database Reset
            // Reset sequence settings and clear some data
            await _settingsService.ResetDatabaseSequencesAsync();

            // 10.3 Restore database back
            await _backupService.RestoreBackupAsync(backupPath);

            var roomsAfter = await _roomService.GetRoomsAsync();
            Assert.Equal(2, roomsAfter.Count());
            Assert.Equal("โรงแรม แสนสุข E2E", await _settingsService.GetShopNameAsync());
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }

        // ==========================================
        // STAGE 11: Audit Logs Inspection
        // ==========================================
        var auditLogs = (await _auditService.GetLogsAsync()).ToList();
        Assert.NotEmpty(auditLogs);
        
        // Assert that backup/restore logs, check-ins, or other operations are present
        var backupLogs = auditLogs.Where(x => x.Action.Contains("สำรองข้อมูล") || x.Action.Contains("คืนค่า"));
        Assert.NotEmpty(backupLogs);
    }

    private void SignLicense(LicenseFile lic)
    {
        string signableData = lic.GetSignableData();
        byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(TestPrivateKeyBase64), out _);
        byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        lic.Signature = Convert.ToBase64String(signatureBytes);
    }

    private void SignRevocation(RevocationListFile rev)
    {
        string signableData = rev.GetSignableData();
        byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(TestPrivateKeyBase64), out _);
        byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        rev.Signature = Convert.ToBase64String(signatureBytes);
    }

    private void ClearTestRegistryAndFiles(bool deleteDb = false)
    {
        if (deleteDb && File.Exists(_tempDbPath)) File.Delete(_tempDbPath);

        string hiddenFilePath = Path.Combine(_tempLicenseFolder, TrialManager.HiddenFileName);
        if (File.Exists(hiddenFilePath))
        {
            File.SetAttributes(hiddenFilePath, FileAttributes.Normal);
            File.Delete(hiddenFilePath);
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TrialManager.RegistrySubKey, throwOnMissingSubKey: false);
        }
        catch { }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        ClearTestRegistryAndFiles(deleteDb: true);

        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (Directory.Exists(_tempLicenseFolder))
                {
                    Directory.Delete(_tempLicenseFolder, recursive: true);
                }
                if (Directory.Exists(_tempLogPath))
                {
                    Directory.Delete(_tempLogPath, recursive: true);
                }
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
