using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace HotelPOS.Licensing;

public class UsbDriveInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string PhysicalSerial { get; set; } = string.Empty;
    public string UsbHardwareId { get; set; } = string.Empty;
}

public static class UsbDongleManager
{
    public const string DongleFileName = "dongle.key";

    /// <summary>
    /// ดึงรายการ USB Removable Drives ที่เชื่อมต่ออยู่ทั้งหมด พร้อม Physical Hardware Serial ระดับชิป USB
    /// </summary>
    public static List<UsbDriveInfo> GetConnectedUsbDrives()
    {
        var list = new List<UsbDriveInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Removable)
                {
                    string driveLetter = drive.Name.TrimEnd('\\');
                    string serial = GetPhysicalUsbSerial(driveLetter);
                    string usbHwId = HashUsbSerial(serial);

                    list.Add(new UsbDriveInfo
                    {
                        DriveLetter = driveLetter,
                        VolumeLabel = drive.VolumeLabel,
                        PhysicalSerial = serial,
                        UsbHardwareId = usbHwId
                    });
                }
            }
        }
        catch { }

        return list;
    }

    /// <summary>
    /// สแกนหาไฟล์ dongle.key หรือ license.dat ใน USB Flash Drives ทั้งหมดที่เสียบอยู่
    /// </summary>
    public static (LicenseFile? DongleLicense, UsbDriveInfo? DriveInfo, string? RawContent) ScanForDongleKey()
    {
        var usbDrives = GetConnectedUsbDrives();
        foreach (var usb in usbDrives)
        {
            string donglePath = Path.Combine(usb.DriveLetter + "\\", DongleFileName);
            string licensePath = Path.Combine(usb.DriveLetter + "\\", LicenseManager.LicenseFileName);

            string? targetPath = File.Exists(donglePath) ? donglePath : (File.Exists(licensePath) ? licensePath : null);

            if (targetPath != null)
            {
                try
                {
                    string json = File.ReadAllText(targetPath).Trim();
                    var license = LicenseFile.FromJson(json);
                    if (license != null)
                    {
                        return (license, usb, json);
                    }
                }
                catch { }
            }
        }

        return (null, null, null);
    }

    /// <summary>
    /// อ่านค่า Physical Hardware Serial จาก WMI สำหรับ USB Drive ระดับชิปคอนโทรลเลอร์
    /// </summary>
    public static string GetPhysicalUsbSerial(string driveLetter)
    {
        string cleanLetter = driveLetter.TrimEnd('\\').ToUpperInvariant();
        try
        {
            // Query partitions associated with this logical disk drive letter
            string partitionQuery = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{cleanLetter}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";
            using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);
            using var partitions = partitionSearcher.Get();

            foreach (var partition in partitions)
            {
                string partitionId = partition["DeviceID"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(partitionId)) continue;

                // Query physical disk drives associated with this partition
                string diskQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                using var diskSearcher = new ManagementObjectSearcher(diskQuery);
                using var disks = diskSearcher.Get();

                foreach (var disk in disks)
                {
                    var pnpId = disk["PNPDeviceID"]?.ToString() ?? "";
                    var serial = disk["SerialNumber"]?.ToString() ?? "";

                    // หากมี SerialNumber ระดับชิปโดยตรง
                    if (!string.IsNullOrWhiteSpace(serial) && serial.Length >= 5)
                    {
                        return serial.Trim();
                    }

                    // หากไม่มี SerialNumber ให้อ่านจาก PNPDeviceID
                    if (!string.IsNullOrWhiteSpace(pnpId))
                    {
                        // PNPDeviceID มักอยู่ในรูป USBSTOR\DISK&VEN_...\[SERIAL_NUMBER]
                        var parts = pnpId.Split('\\');
                        if (parts.Length > 0)
                        {
                            var lastPart = parts[parts.Length - 1];
                            if (lastPart.Contains("&"))
                            {
                                var subParts = lastPart.Split('&');
                                lastPart = subParts[0];
                            }
                            if (!string.IsNullOrWhiteSpace(lastPart))
                            {
                                return lastPart.Trim();
                            }
                        }
                        return pnpId.Trim();
                    }
                }
            }
        }
        catch { }

        // Fallback 1: ค้นหาใน USB Storage ทั่วไปหากความเชื่อมโยงของ WMI พัง
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, PNPDeviceID, SerialNumber, InterfaceType FROM Win32_DiskDrive WHERE InterfaceType = 'USB'");
            using var collection = searcher.Get();

            foreach (var drive in collection)
            {
                var pnpId = drive["PNPDeviceID"]?.ToString() ?? "";
                var serial = drive["SerialNumber"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(serial) && serial.Length >= 5)
                {
                    return serial.Trim();
                }
            }
        }
        catch { }

        // Fallback 2: กรณี WMI อ่านไม่ได้ ให้ใช้ Drive Volume Label + Letter
        return $"GENERIC-USB-{cleanLetter}";
    }

    public static string HashUsbSerial(string rawSerial)
    {
        string combined = $"USB_CHIP_SERIAL:{rawSerial.Trim().ToUpperInvariant()}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// ฟอร์แมต USB Flash Drive (Quick Format) เป็นระบบไฟล์ FAT32 หรือ NTFS พร้อมตั้งชื่อ Volume Label
    /// </summary>
    public static bool FormatUsbDrive(string driveLetter, string fileSystem = "FAT32", string volumeLabel = "REST_RENT_KEY")
    {
        try
        {
            string cleanLetter = driveLetter.TrimEnd('\\').Replace(":", "").Trim();
            if (string.IsNullOrEmpty(cleanLetter)) return false;

            // 1. ลองใช้ PowerShell Format-Volume ก่อน
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Format-Volume -DriveLetter '{cleanLetter}' -FileSystem {fileSystem} -NewFileSystemLabel '{volumeLabel}' -Confirm:$false -Force\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                proc?.WaitForExit(40000);
                if (proc?.ExitCode == 0) return true;
            }

            // 2. Fallback: ใช้ cmd.exe format command (กรณี PowerShell โดนล็อก)
            var psiCmd = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c format {cleanLetter}: /FS:{fileSystem} /V:{volumeLabel} /Q /Y",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var procCmd = System.Diagnostics.Process.Start(psiCmd))
            {
                procCmd?.WaitForExit(40000);
                return procCmd?.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}
