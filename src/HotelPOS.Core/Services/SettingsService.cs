using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly IAppLogger _logger;

    // ใช้ล็อกป้องกันการออกเลขที่เอกสารซ้ำกันเวลามีคนกดพิมพ์พร้อมกันหลายเครื่อง (LAN)
    private static readonly SemaphoreSlim DocumentNumberLock = new(1, 1);

    public SettingsService(ISettingsRepository repository, IAppLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<string?> GetShopNameAsync() => GetAsync("shop_name");

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _repository.GetByKeyAsync(key);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string? value)
    {
        await _repository.UpsertAsync(key, value);
    }

    public async Task<string> GetNextDocumentNumberAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        await DocumentNumberLock.WaitAsync();
        try
        {
            var prefixSetting = await _repository.GetByKeyAsync("receipt_doc_prefix");
            var runningSetting = await _repository.GetByKeyAsync("receipt_doc_running_number");

            var prefix = prefixSetting?.Value ?? "RC";
            var current = int.TryParse(runningSetting?.Value, out var n) ? n : 0;
            var next = current + 1;

            await _repository.UpsertAsync("receipt_doc_running_number", next.ToString());

            var documentNumber = $"{prefix}-{next:D6}";
            _logger.Info(LogCategory.System, $"ออกเลขที่เอกสารใหม่: {documentNumber}", correlationId);
            return documentNumber;
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.System, "ออกเลขที่เอกสารไม่สำเร็จ", ex, correlationId);
            throw;
        }
        finally
        {
            DocumentNumberLock.Release();
        }
    }

    public async Task<SystemSettingsDto> GetAllSettingsAsync()
    {
        var dto = new SystemSettingsDto();

        dto.ShopName = await GetAsync("shop_name") ?? dto.ShopName;
        dto.ShopAddress = await GetAsync("shop_address") ?? dto.ShopAddress;
        dto.ShopPhone = await GetAsync("shop_phone") ?? dto.ShopPhone;
        dto.ShopTaxId = await GetAsync("shop_tax_id") ?? dto.ShopTaxId;
        dto.BillHeader = await GetAsync("bill_header") ?? dto.BillHeader;
        dto.BillFooter = await GetAsync("bill_footer") ?? dto.BillFooter;

        dto.PrinterName = await GetAsync("printer_name") ?? dto.PrinterName;
        dto.PaperType = await GetAsync("paper_type") ?? dto.PaperType;
        dto.AutoPrintOnCheckout = bool.TryParse(await GetAsync("auto_print_on_checkout"), out var ap) ? ap : dto.AutoPrintOnCheckout;
        dto.ShowSignatureBox = bool.TryParse(await GetAsync("show_signature_box"), out var sb) ? sb : dto.ShowSignatureBox;
        dto.PrinterFeedLines = int.TryParse(await GetAsync("printer_feed_lines"), out var fl) ? fl : dto.PrinterFeedLines;
        dto.PrinterAutoCut = bool.TryParse(await GetAsync("printer_auto_cut"), out var ac) ? ac : dto.PrinterAutoCut;

        dto.DefaultCheckInTime = await GetAsync("default_checkin_time") ?? dto.DefaultCheckInTime;
        dto.DefaultCheckOutTime = await GetAsync("default_checkout_time") ?? dto.DefaultCheckOutTime;
        dto.DefaultSecurityDeposit = decimal.TryParse(await GetAsync("default_security_deposit"), out var dep) ? dep : dto.DefaultSecurityDeposit;
        dto.VatRate = decimal.TryParse(await GetAsync("vat_rate"), out var vat) ? vat : dto.VatRate;
        dto.EnableVat = bool.TryParse(await GetAsync("enable_vat"), out var ev) ? ev : dto.EnableVat;

        dto.LogoImagePath = await GetAsync("logo_image_path");
        dto.QrCodeImagePath = await GetAsync("qrcode_image_path");

        dto.ReceiptDocPrefix = await GetAsync("receipt_doc_prefix") ?? dto.ReceiptDocPrefix;
        dto.ReceiptDocRunningNumber = int.TryParse(await GetAsync("receipt_doc_running_number"), out var rn) ? rn : dto.ReceiptDocRunningNumber;

        // ค่าสาธารณูปโภค
        dto.ElectricBillingMode = await GetAsync("electric_billing_mode") ?? dto.ElectricBillingMode;
        dto.ElectricRatePerUnit = decimal.TryParse(await GetAsync("electric_rate_per_unit"), out var elec) ? elec : dto.ElectricRatePerUnit;
        dto.ElectricFlatRate = decimal.TryParse(await GetAsync("electric_flat_rate"), out var eFlat) ? eFlat : dto.ElectricFlatRate;
        dto.WaterBillingMode = await GetAsync("water_billing_mode") ?? dto.WaterBillingMode;
        dto.WaterRatePerUnit = decimal.TryParse(await GetAsync("water_rate_per_unit"), out var water) ? water : dto.WaterRatePerUnit;
        dto.WaterFlatRatePerPerson = decimal.TryParse(await GetAsync("water_flat_rate_per_person"), out var wFlat) ? wFlat : dto.WaterFlatRatePerPerson;
        dto.CommonAreaFee = decimal.TryParse(await GetAsync("common_area_fee"), out var caf) ? caf : dto.CommonAreaFee;
        dto.GarbageFee = decimal.TryParse(await GetAsync("garbage_fee"), out var gf) ? gf : dto.GarbageFee;
        dto.LobbyTerms = await GetAsync("lobby_terms") ?? dto.LobbyTerms;

        // Auto Backup
        dto.AutoBackupEnabled = bool.TryParse(await GetAsync("auto_backup_enabled"), out var abe) ? abe : dto.AutoBackupEnabled;
        dto.AutoBackupOnExit = bool.TryParse(await GetAsync("auto_backup_on_exit"), out var abex) ? abex : dto.AutoBackupOnExit;
        dto.AutoBackupMaxKeepFiles = int.TryParse(await GetAsync("auto_backup_max_keep_files"), out var abm) ? abm : dto.AutoBackupMaxKeepFiles;
        dto.CustomBackupFolderPath = await GetAsync("custom_backup_folder_path") ?? dto.CustomBackupFolderPath;

        // App Theme & Font Size
        dto.AppTheme = await GetAsync("app_theme") ?? dto.AppTheme;
        dto.AppFontSize = await GetAsync("app_font_size") ?? dto.AppFontSize;

        return dto;
    }

    public async Task SaveAllSettingsAsync(SystemSettingsDto settings)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            await SetAsync("shop_name", settings.ShopName);
            await SetAsync("shop_address", settings.ShopAddress);
            await SetAsync("shop_phone", settings.ShopPhone);
            await SetAsync("shop_tax_id", settings.ShopTaxId);
            await SetAsync("bill_header", settings.BillHeader);
            await SetAsync("bill_footer", settings.BillFooter);

            await SetAsync("printer_name", settings.PrinterName);
            await SetAsync("paper_type", settings.PaperType);
            await SetAsync("auto_print_on_checkout", settings.AutoPrintOnCheckout.ToString());
            await SetAsync("show_signature_box", settings.ShowSignatureBox.ToString());
            await SetAsync("printer_feed_lines", settings.PrinterFeedLines.ToString());
            await SetAsync("printer_auto_cut", settings.PrinterAutoCut.ToString());

            await SetAsync("default_checkin_time", settings.DefaultCheckInTime);
            await SetAsync("default_checkout_time", settings.DefaultCheckOutTime);
            await SetAsync("default_security_deposit", settings.DefaultSecurityDeposit.ToString());
            await SetAsync("vat_rate", settings.VatRate.ToString());
            await SetAsync("enable_vat", settings.EnableVat.ToString());

            await SetAsync("logo_image_path", settings.LogoImagePath);
            await SetAsync("qrcode_image_path", settings.QrCodeImagePath);

            await SetAsync("receipt_doc_prefix", settings.ReceiptDocPrefix);
            await SetAsync("receipt_doc_running_number", settings.ReceiptDocRunningNumber.ToString());

            // ค่าสาธารณูปโภค
            await SetAsync("electric_billing_mode", settings.ElectricBillingMode);
            await SetAsync("electric_rate_per_unit", settings.ElectricRatePerUnit.ToString());
            await SetAsync("electric_flat_rate", settings.ElectricFlatRate.ToString());
            await SetAsync("water_billing_mode", settings.WaterBillingMode);
            await SetAsync("water_rate_per_unit", settings.WaterRatePerUnit.ToString());
            await SetAsync("water_flat_rate_per_person", settings.WaterFlatRatePerPerson.ToString());
            await SetAsync("common_area_fee", settings.CommonAreaFee.ToString());
            await SetAsync("garbage_fee", settings.GarbageFee.ToString());
            await SetAsync("lobby_terms", settings.LobbyTerms);

            // Auto Backup
            await SetAsync("auto_backup_enabled", settings.AutoBackupEnabled.ToString());
            await SetAsync("auto_backup_on_exit", settings.AutoBackupOnExit.ToString());
            await SetAsync("auto_backup_max_keep_files", settings.AutoBackupMaxKeepFiles.ToString());
            await SetAsync("custom_backup_folder_path", settings.CustomBackupFolderPath);

            // App Theme & Font Size
            await SetAsync("app_theme", settings.AppTheme);
            await SetAsync("app_font_size", settings.AppFontSize);

            _logger.Info(LogCategory.System, "บันทึกการตั้งค่าระบบเรียบร้อยแล้ว", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.System, "บันทึกการตั้งค่าระบบไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task ResetDatabaseSequencesAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            await _repository.ResetDatabaseSequencesAsync();
            await SetAsync("receipt_doc_running_number", "0");
            _logger.Info(LogCategory.System, "รีเซ็ตคีย์หลักในฐานข้อมูลและตั้งเลขรันบิลเริ่มต้นใหม่สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.System, "รีเซ็ตคีย์หลักในฐานข้อมูลและเลขรันบิลไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task ZetZeroDatabaseAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            await _repository.ZetZeroDatabaseAsync();
            await SetAsync("receipt_doc_running_number", "0");
            _logger.Info(LogCategory.System, "ล้างธุรกรรมระบบทั้งหมดเป็น 0 (Set Zero) และรีเซ็ตเลขรันบิลเริ่มต้นใหม่สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.System, "ล้างธุรกรรมระบบทั้งหมดเป็น 0 ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
