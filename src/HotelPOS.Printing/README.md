# HotelPOS.Printing

โมดูลนี้ยังไม่มีโค้ด — จะเขียนใน **Phase 4: บิล/พิมพ์เอกสาร** ตาม Roadmap (PROJECT_PLAN.md ข้อ 8)

โครงที่วางแผนไว้:
- `IPrintEngine.cs` — interface กลาง (PrintReceipt, PrintA4Invoice, PreviewDocument)
- `EscPosBuilder.cs` — สร้างคำสั่ง ESC/POS สำหรับ slip printer 58mm/80mm
- `A4DocumentBuilder.cs` — ใช้ QuestPDF สร้างเอกสาร A4 (ใบกำกับภาษีเต็มรูป)
- `RawPrinterHelper.cs` — ส่งข้อมูลดิบไปเครื่องพิมพ์ผ่าน Win32 API (winspool.drv)
- `Templates/` — เทมเพลตเอกสารแต่ละแบบ ดึงชื่อร้าน/โลโก้จาก settings เสมอ (ห้าม hardcode ตาม SKILL.md ข้อ 7)
