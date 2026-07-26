using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface ISettingsService
{
    Task<string?> GetShopNameAsync();
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);

    Task<SystemSettingsDto> GetAllSettingsAsync();
    Task SaveAllSettingsAsync(SystemSettingsDto settings);

    /// <summary>ออกเลขที่เอกสารถัดไป (เช่น RC-000123) แบบ thread-safe ต่อการเรียกครั้งเดียว</summary>
    Task<string> GetNextDocumentNumberAsync();
}
