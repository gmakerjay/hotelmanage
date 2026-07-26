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

        dto.DefaultCheckInTime = await GetAsync("default_checkin_time") ?? dto.DefaultCheckInTime;
        dto.DefaultCheckOutTime = await GetAsync("default_checkout_time") ?? dto.DefaultCheckOutTime;
        dto.DefaultSecurityDeposit = decimal.TryParse(await GetAsync("default_security_deposit"), out var dep) ? dep : dto.DefaultSecurityDeposit;
        dto.VatRate = decimal.TryParse(await GetAsync("vat_rate"), out var vat) ? vat : dto.VatRate;
        dto.EnableVat = bool.TryParse(await GetAsync("enable_vat"), out var ev) ? ev : dto.EnableVat;

        dto.LogoImagePath = await GetAsync("logo_image_path");
        dto.QrCodeImagePath = await GetAsync("qrcode_image_path");

        dto.ReceiptDocPrefix = await GetAsync("receipt_doc_prefix") ?? dto.ReceiptDocPrefix;
        dto.ReceiptDocRunningNumber = int.TryParse(await GetAsync("receipt_doc_running_number"), out var rn) ? rn : dto.ReceiptDocRunningNumber;

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

            await SetAsync("default_checkin_time", settings.DefaultCheckInTime);
            await SetAsync("default_checkout_time", settings.DefaultCheckOutTime);
            await SetAsync("default_security_deposit", settings.DefaultSecurityDeposit.ToString());
            await SetAsync("vat_rate", settings.VatRate.ToString());
            await SetAsync("enable_vat", settings.EnableVat.ToString());

            await SetAsync("logo_image_path", settings.LogoImagePath);
            await SetAsync("qrcode_image_path", settings.QrCodeImagePath);

            await SetAsync("receipt_doc_prefix", settings.ReceiptDocPrefix);
            await SetAsync("receipt_doc_running_number", settings.ReceiptDocRunningNumber.ToString());

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
}
