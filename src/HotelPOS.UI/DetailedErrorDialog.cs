using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HotelPOS.Logging;

namespace HotelPOS.UI;

/// <summary>
/// หน้าต่างแสดงรายละเอียดข้อผิดพลาดของระบบแบบละเอียดลึกซึ้ง (Thorough Error Popup)
/// พร้อมปุ่มคัดลอกรายละเอียด และปุ่มเปิดโฟลเดอร์ Log
/// </summary>
public class DetailedErrorDialog : Form
{
    private readonly Exception _exception;
    private readonly string _userFriendlyMessage;
    private readonly string _logFolder;
    private readonly string _fullDiagnosticDetails;

    public DetailedErrorDialog(Exception exception, string userFriendlyMessage, string logFolder)
    {
        _exception = exception ?? new Exception("Unknown Exception");
        _userFriendlyMessage = userFriendlyMessage;
        _logFolder = logFolder;
        _fullDiagnosticDetails = BuildFullDiagnosticReport(_exception, _userFriendlyMessage);

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = "HotelPOS TH - รายงานข้อผิดพลาดระบบอย่างละเอียด";
        Size = new Size(820, 620);
        MinimumSize = new Size(680, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(248, 250, 252);
        Icon = SystemIcons.Error;

        // 1. Header Banner (Red/Crimson)
        var bannerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(185, 28, 28), // Deep Red
            Padding = new Padding(20, 15, 20, 15)
        };

        var lblBannerTitle = new Label
        {
            Text = "เกิดข้อผิดพลาดในการทำงานของระบบ (System Error Caught)",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblBannerSubtitle = new Label
        {
            Text = "ระบบทำการบันทึกข้อมูลอย่างละเอียดลงใน Log File และแจ้งรายละเอียดเชิงลึกดังต่อไปนี้",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(254, 226, 226),
            Location = new Point(20, 48),
            AutoSize = true
        };

        bannerPanel.Controls.AddRange(new Control[] { lblBannerTitle, lblBannerSubtitle });

        // 2. User Message Summary Box
        var summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(20, 10, 20, 10),
            BackColor = Color.White
        };

        var lblSummaryHeader = new Label
        {
            Text = "รายละเอียดสรุป:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(20, 10),
            AutoSize = true
        };

        var lblSummaryText = new Label
        {
            Text = string.IsNullOrWhiteSpace(_userFriendlyMessage) ? _exception.Message : _userFriendlyMessage,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 38, 38),
            Location = new Point(20, 34),
            Size = new Size(760, 30),
            AutoEllipsis = true
        };

        summaryPanel.Controls.AddRange(new Control[] { lblSummaryHeader, lblSummaryText });

        // 3. Technical Diagnostic Detail (RichTextBox)
        var detailPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblDetailHeader = new Label
        {
            Text = "ข้อมูลทางเทคนิคทุกซอกมุม (Technical Diagnostics & Stack Trace):",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 28
        };

        var rtbDetails = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = _fullDiagnosticDetails,
            Font = new Font("Consolas", 10F, FontStyle.Regular),
            BackColor = Color.FromArgb(15, 23, 42), // Dark Slate / Terminal Code style
            ForeColor = Color.FromArgb(241, 245, 249),
            BorderStyle = BorderStyle.FixedSingle
        };

        detailPanel.Controls.Add(rtbDetails);
        detailPanel.Controls.Add(lblDetailHeader);

        // 4. Bottom Button Panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = Color.White
        };

        var btnCopy = new Button
        {
            Text = "คัดลอกรายละเอียดข้อผิดพลาด",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(230, 40),
            Location = new Point(20, 12),
            Cursor = Cursors.Hand
        };
        btnCopy.FlatAppearance.BorderSize = 0;
        btnCopy.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(_fullDiagnosticDetails);
                MessageBox.Show("คัดลอกข้อมูลข้อผิดพลาดทั้งหมดลงในคลิปบอร์ดเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถคัดลอกได้: {ex.Message}", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var btnOpenLog = new Button
        {
            Text = "เปิดโฟลเดอร์ Log",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 40),
            Location = new Point(260, 12),
            Cursor = Cursors.Hand
        };
        btnOpenLog.FlatAppearance.BorderSize = 0;
        btnOpenLog.Click += (s, e) =>
        {
            try
            {
                if (Directory.Exists(_logFolder))
                {
                    Process.Start("explorer.exe", _logFolder);
                }
                else
                {
                    MessageBox.Show($"ไม่พบโฟลเดอร์ Log ที่: {_logFolder}", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถเปิดโฟลเดอร์ Log ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var btnClose = new Button
        {
            Text = "ปิดหน้าต่าง",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 40),
            Location = new Point(660, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        bottomPanel.Controls.AddRange(new Control[] { btnCopy, btnOpenLog, btnClose });

        Controls.Add(detailPanel);
        Controls.Add(summaryPanel);
        Controls.Add(bannerPanel);
        Controls.Add(bottomPanel);
    }

    public static string BuildFullDiagnosticReport(Exception ex, string? userContext = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"[HOTELPOS SYSTEM DIAGNOSTIC ERROR REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}]");
        sb.AppendLine("================================================================================");
        if (!string.IsNullOrWhiteSpace(userContext))
        {
            sb.AppendLine($"USER CONTEXT / MESSAGE: {userContext}");
            sb.AppendLine("--------------------------------------------------------------------------------");
        }

        // Environment Details
        sb.AppendLine("[SYSTEM & ENVIRONMENT]");
        sb.AppendLine($"OS Version       : {Environment.OSVersion} (64-bit OS: {Environment.Is64BitOperatingSystem})");
        sb.AppendLine($"Machine Name     : {Environment.MachineName}");
        sb.AppendLine($"Process ID       : {Environment.ProcessId} (64-bit Process: {Environment.Is64BitProcess})");
        sb.AppendLine($"Working Set Mem  : {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024):N0} MB");
        sb.AppendLine($"Current Thread   : ID {Environment.CurrentManagedThreadId} (Name: {System.Threading.Thread.CurrentThread.Name ?? "Main/ThreadPool"})");
        sb.AppendLine($"Current User ID  : {LogContext.CurrentUserId?.ToString() ?? "Guest/Not set"}");
        sb.AppendLine($"Machine Guid ID  : {LogContext.MachineId}");
        sb.AppendLine("--------------------------------------------------------------------------------");

        // Exception Hierarchy
        int level = 1;
        Exception? currentEx = ex;
        while (currentEx != null)
        {
            sb.AppendLine($"[EXCEPTION LEVEL {level++}]");
            sb.AppendLine($"Type      : {currentEx.GetType().FullName}");
            sb.AppendLine($"Message   : {currentEx.Message}");
            sb.AppendLine($"Source    : {currentEx.Source ?? "-"}");
            sb.AppendLine($"TargetSite: {currentEx.TargetSite?.ToString() ?? "-"}");
            if (currentEx.Data.Count > 0)
            {
                sb.AppendLine("Data Collection:");
                foreach (System.Collections.DictionaryEntry entry in currentEx.Data)
                {
                    sb.AppendLine($"  - {entry.Key}: {entry.Value}");
                }
            }
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(string.IsNullOrWhiteSpace(currentEx.StackTrace) ? "  (No stack trace available)" : currentEx.StackTrace);
            sb.AppendLine("--------------------------------------------------------------------------------");

            currentEx = currentEx.InnerException;
        }

        sb.AppendLine("================================================================================");
        return sb.ToString();
    }
}
