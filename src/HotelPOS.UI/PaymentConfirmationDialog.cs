using System;
using System.Drawing;
using System.Windows.Forms;

namespace HotelPOS.UI;

public class PaymentConfirmationDialog : Form
{
    public PaymentConfirmationDialog(
        string roomNumber,
        string tenantName,
        decimal roomRate,
        decimal elecAmount,
        string elecDetailText,
        decimal waterAmount,
        string waterDetailText,
        decimal commonAreaFee,
        decimal garbageFee,
        decimal extraCharges,
        decimal discountAmount,
        decimal totalAmount)
    {
        Text = "ยืนยันรับชำระเงิน - HotelPOS";
        Size = new Size(550, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblHeaderTitle = new Label
        {
            Text = "ทวนสรุปรายการรับชำระเงิน",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 10),
            AutoSize = true
        };

        var lblHeaderSub = new Label
        {
            Text = "กรุณาตรวจสอบรายละเอียดและยอดเงินสุทธิก่อนกดบันทึกรับชำระเงิน",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(20, 42),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblHeaderTitle);
        pnlHeader.Controls.Add(lblHeaderSub);

        // Main Container
        var pnlBody = new Panel
        {
            Location = new Point(16, 88),
            Size = new Size(502, 442),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        // Tenant Card
        var pnlTenantCard = new Panel
        {
            Size = new Size(468, 48),
            Location = new Point(16, 14),
            BackColor = Color.FromArgb(239, 246, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 8, 10, 8)
        };

        var lblTenantInfo = new Label
        {
            Text = $"ห้องพัก: {roomNumber}   |   ผู้เช่า: {tenantName}",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlTenantCard.Controls.Add(lblTenantInfo);

        // Expense List Panel
        var pnlExpenses = new FlowLayoutPanel
        {
            Location = new Point(16, 68),
            Size = new Size(468, 235),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(4)
        };

        void AddExpenseRow(string title, string detail, decimal amount, bool isDiscount = false)
        {
            var rowPnl = new Panel { Size = new Size(456, 32), Margin = new Padding(0, 0, 0, 4) };
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(0, 5),
                AutoSize = true
            };
            var lblDetail = new Label
            {
                Text = detail,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(145, 6),
                AutoSize = true
            };
            var lblAmt = new Label
            {
                Text = $"{(isDiscount ? "-" : "")}{amount:N2} บาท",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = isDiscount ? Color.FromArgb(220, 38, 38) : Color.FromArgb(15, 23, 42),
                Size = new Size(150, 26),
                Location = new Point(306, 3),
                TextAlign = ContentAlignment.MiddleRight
            };

            rowPnl.Controls.Add(lblTitle);
            rowPnl.Controls.Add(lblDetail);
            rowPnl.Controls.Add(lblAmt);
            pnlExpenses.Controls.Add(rowPnl);
        }

        AddExpenseRow("ค่าเช่าห้องพัก:", "", roomRate);
        AddExpenseRow("ค่าไฟฟ้า:", elecDetailText, elecAmount);
        AddExpenseRow("ค่าน้ำประปา:", waterDetailText, waterAmount);

        if (commonAreaFee + garbageFee > 0)
        {
            AddExpenseRow("ค่าส่วนกลาง/ขยะ:", "รวมค่าขยะเเละบริการ", commonAreaFee + garbageFee);
        }
        if (extraCharges > 0)
        {
            AddExpenseRow("ค่าบริการเพิ่ม:", "รายการเสริม", extraCharges);
        }
        if (discountAmount > 0)
        {
            AddExpenseRow("ส่วนลดพิเศษ:", "หักส่วนลด", discountAmount, true);
        }

        // Net Total Card (Highlight)
        var pnlTotalCard = new Panel
        {
            Location = new Point(16, 312),
            Size = new Size(468, 62),
            BackColor = Color.FromArgb(240, 253, 244),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblTotalLabel = new Label
        {
            Text = "ยอดสุทธิที่ต้องรับชำระ:",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 101, 52),
            Location = new Point(10, 18),
            AutoSize = true
        };

        var lblTotalVal = new Label
        {
            Text = $"{totalAmount:N2} บาท",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74),
            Size = new Size(250, 38),
            Location = new Point(208, 9),
            TextAlign = ContentAlignment.MiddleRight
        };

        pnlTotalCard.Controls.Add(lblTotalLabel);
        pnlTotalCard.Controls.Add(lblTotalVal);

        // Status Note
        var lblStatusNote = new Label
        {
            Text = "สถานะหลังบันทึก: ชำระแล้วเรียบร้อย (สลับเป็นสถานะปกติ ไม่มีภาระค้างชำระ)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 101, 52),
            Location = new Point(16, 395),
            AutoSize = true
        };

        pnlBody.Controls.Add(pnlTenantCard);
        pnlBody.Controls.Add(pnlExpenses);
        pnlBody.Controls.Add(pnlTotalCard);
        pnlBody.Controls.Add(lblStatusNote);

        // Footer Action Buttons
        var btnConfirm = new Button
        {
            Text = "ยืนยันรับชำระเงินทันที",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(210, 44),
            Location = new Point(148, 542),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Yes
        };
        btnConfirm.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text = "ยกเลิก / แก้ไข",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(135, 44),
            Location = new Point(368, 542),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.No
        };

        Controls.Add(pnlHeader);
        Controls.Add(pnlBody);
        Controls.Add(btnConfirm);
        Controls.Add(btnCancel);
    }
}
