# HotelPOS TH — Source Code Scaffold (Phase 0: Setup)

โครงโปรเจคนี้คือผลลัพธ์ของ **Phase 0** ตาม Roadmap ใน `PROJECT_PLAN.md` ข้อ 8
ครอบคลุม: โครงสร้าง Solution ทั้งหมด, Database Schema เต็ม, ระบบ Logging (Serilog) ที่ใช้งานได้จริง,
และตัวอย่าง Repository → Service → UI ครบ 1 วงจร (โมดูล Settings) เพื่อเป็นแม่แบบให้เฟสถัดไปเดินตาม

## สิ่งที่ทำงานได้แล้วในเวอร์ชันนี้
- เปิดโปรแกรม (`HotelPOS.UI`) แล้วจะสร้างฐานข้อมูล SQLite ที่ `%AppData%\HotelPOS\hotelpos.db` อัตโนมัติจาก `schema.sql`
- Log ทุกอย่างลง `%AppData%\HotelPOS\logs\app-log-YYYYMMDD.json` แบบ structured JSON
- มี Global Exception Handler ดัก error ไม่ให้โปรแกรม crash เงียบๆ
- หน้าหลักแสดง "ชื่อร้าน" ที่ดึงจากฐานข้อมูลจริง (ผ่าน SettingsService) ยืนยันว่าทุกชั้นเชื่อมกันได้

## สิ่งที่ยังไม่ทำ (รอ Phase ถัดไป — ดูรายละเอียดใน SKILL.md ข้อ 8)
- โมดูลห้องพัก/จอง (Room Grid, เช็คอิน/เช็คเอาท์)
- โมดูล POS ขายสินค้า
- ระบบ License (Core + Admin Tool) — ตอนนี้เป็นแค่โฟลเดอร์เปล่า+README แผนงาน
- ระบบพิมพ์เอกสาร (Receipt/A4)
- รายงาน, Backup/Restore/Reset

## วิธี Build (ต้องใช้ Windows + Visual Studio 2022 หรือ .NET 8 SDK)
```bash
# ต้องมี .NET 8 SDK ติดตั้งก่อน (รันบนเครื่องพัฒนา ไม่ใช่เครื่องลูกค้า)
dotnet restore
dotnet build
dotnet test                     # รัน unit test ของ SettingsService
dotnet run --project src/HotelPOS.UI
```

> หมายเหตุ: โปรเจคนี้ต้องใช้ NuGet packages (Dapper, Microsoft.Data.Sqlite, Serilog, QuestPDF, xUnit ฯลฯ)
> ต้องรัน `dotnet restore` บนเครื่องที่ต่ออินเทอร์เน็ตไปยัง nuget.org ได้ก่อน build ครั้งแรก

## โครงสร้างโฟลเดอร์
ดูรายละเอียดเต็มใน `SKILL.md` ข้อ 3 — สรุปสั้นๆ:
```
src/HotelPOS.Common/       Models, Enums (ไม่มี logic)
src/HotelPOS.Data/         Database schema + Repository (Dapper + SQLite)
src/HotelPOS.Core/         Business logic / Services
src/HotelPOS.Logging/      Serilog wrapper (IAppLogger) — ทุกโปรเจคเรียกผ่านนี้เท่านั้น
src/HotelPOS.Licensing/    ระบบตรวจสอบลิขสิทธิ์และซิงค์ความปลอดภัยโหมด Trial 30 วัน
src/HotelPOS.Printing/     (ว่าง รอ Phase 4)
src/HotelPOS.UI/           WinForms (หน้าจอ) — โปรแกรมหลักที่รันบนเครื่องลูกค้า
src/HotelPOS.LicenseAdminTool/ เครื่องมืออกรหัสใบอนุญาตและเซ็นชื่อดิจิทัลฝั่งผู้ขาย (Admin Tool)
tests/HotelPOS.Tests/      Unit tests (xUnit) ทั้งระบบ
Docs/                      โฟลเดอร์เอกสารประวัติและความคืบหน้าของโครงการ
```

## 📂 เอกสารโครงการย้อนหลังและรายงานความคืบหน้า
สามารถติดตามรายละเอียดเพิ่มเติมได้ที่โฟลเดอร์ `Docs/`:
- 📄 **ประวัติการปรับปรุงโค้ดทั้งหมด (Changelogs):** [CHANGELOG.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/Changelogs/CHANGELOG.md)
- 📊 **รายงานความคืบหน้าภาพรวม (Progress Report):** [PROGRESS.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/Progress/PROGRESS.md)

## ขั้นต่อไปที่แนะนำ
พิมพ์บอกได้เลยว่าจะให้เขียนต่อในเฟสถัดไป เช่น "เริ่มพัฒนา Phase 2 (ระบบห้องพักและการจอง)" หรือมีจุดใดที่ต้องการปรับแก้ต่อครับ
