---
name: hotel-pos-builder
description: >
  ใช้ Skill นี้ทุกครั้งที่ต้องเขียน/แก้ไข/ต่อยอดโค้ดของโปรเจค "HotelPOS TH"
  ซึ่งเป็นระบบ POS + บริหารห้องพักสำหรับธุรกิจที่พักขนาดเล็ก-กลาง
  ครอบคลุม: โมดูลห้องพัก/จอง, POS ขายสินค้า, ระบบบิล/พิมพ์เอกสาร (Receipt+A4),
  ระบบ License/Activation, ระบบ Logger, Backup/Restore, รายงาน, ตั้งค่าระบบ,
  และ License Admin Tool ฝั่งผู้ขาย ใช้ทุกครั้งที่มีการอ้างถึงไฟล์/โฟลเดอร์ในโปรเจคนี้
  ชื่อ HotelPOS, HotelPOS.UI, HotelPOS.Core, HotelPOS.Licensing, HotelPOS.LicenseAdminTool
  หรือมีการพูดถึง "ระบบห้องพัก", "ระบบขายห้องพัก", "Hotel POS"
---

# Hotel POS Builder — Agent Skill

Skill นี้คือ "คู่มือการทำงาน" สำหรับ Agent ที่เขียนโค้ดโปรเจคนี้ ให้ยึดตามนี้เสมอ
ไม่ใช่แค่ทำตาม prompt ผิวเผิน — อ่านให้ครบก่อนเริ่มเขียนโค้ดทุกครั้ง

## 1. บริบทโปรเจค (อ่านก่อนเสมอ)
ก่อนเริ่มงานใดๆ ให้เปิดอ่านไฟล์ `PROJECT_PLAN.md` ที่อยู่ root ของโปรเจคก่อนทุกครั้ง
(ถ้ายังไม่มีไฟล์นี้ในโปรเจค ให้ถามผู้ใช้หรือสร้างจากบทสนทนาก่อน)
ไฟล์นั้นคือ source of truth ของ: feature list, database schema, license design, logging design

## 2. Tech Stack ตายตัว (ห้ามเปลี่ยนโดยไม่ถามผู้ใช้ก่อน)
- ภาษา: **C# (.NET 8)**
- UI: **WinForms** (ไม่ใช้ WPF/Electron/Web — เพื่อความเบาและรองรับเครื่องเก่า)
- Database: **SQLite** ผ่าน `Microsoft.Data.Sqlite` + `Dapper` (ห้ามใช้ Entity Framework เต็มรูปแบบ เพราะหนักเกินความจำเป็น)
- Logging: **Serilog** (structured JSON, rolling file)
- PDF: **QuestPDF**
- Excel: **ClosedXML**
- Printing (Receipt/A4): `System.Drawing.Printing` + RawPrinterHelper (ESC/POS) สำหรับ slip printer
- Encryption/License: `System.Security.Cryptography` (RSA signature + AES)

## 3. โครงสร้างโปรเจค (บังคับ)
```
HotelPOS.sln
├── src/
│   ├── HotelPOS.UI/              # WinForms Forms/UserControls เท่านั้น ห้ามมี business logic
│   ├── HotelPOS.Core/            # Services, Business rules (BookingService, SalesService ฯลฯ)
│   ├── HotelPOS.Data/            # Repository pattern, SQL, Migrations (ใช้ DbUp หรือ script .sql เอง)
│   ├── HotelPOS.Licensing/       # ระบบ license/activation ฝั่ง client (มี Public Key เท่านั้น)
│   ├── HotelPOS.Printing/        # PrintEngine, Templates
│   ├── HotelPOS.Logging/         # Serilog wrapper, LogViewer control
│   ├── HotelPOS.Common/          # Models, DTOs, Enums, Constants, Extensions
│   └── HotelPOS.LicenseAdminTool/ # โปรแกรมแยก (มี Private Key) — ห้าม reference จาก HotelPOS.UI เด็ดขาด
├── tests/
│   └── HotelPOS.Tests/           # Unit tests (xUnit) โดยเฉพาะ Licensing และ Core
├── PROJECT_PLAN.md
└── SKILL.md
```

**กฎเหล็ก:** `HotelPOS.UI` ห้าม query database ตรงๆ ต้องผ่าน `HotelPOS.Core` → `HotelPOS.Data` เท่านั้น (แยกชั้นชัดเจน เพื่อแก้บัคง่าย/เทสง่าย)

## 4. Coding Conventions
- Naming: PascalCase สำหรับ class/method, camelCase สำหรับตัวแปร local, `_camelCase` สำหรับ private field
- ทุก Service method ที่อาจล้มเหลว ต้อง try-catch แล้ว **log ผ่าน `HotelPOS.Logging`** (ห้ามปล่อย exception เงียบ, ห้ามใช้ `Console.WriteLine` หรือ `Debug.WriteLine` เด็ดขาด — ใช้ logger เท่านั้น)
- ทุกจุดที่มีผลกระทบต่อข้อมูลสำคัญ (ขาย, จอง, ลบ, backup/restore) ต้องมี `correlation_id` เดียวกันตลอด flow (ดูหัวข้อ Logger ใน PROJECT_PLAN.md ข้อ 7.4)
- ข้อความในโค้ด comment เขียนได้ทั้งไทย/อังกฤษ แต่ **ข้อความที่ผู้ใช้เห็น (UI, ใบเสร็จ, error message) ต้องเป็นภาษาไทยเสมอ** ยกเว้นมีการทำ multi-language ในอนาคต
- ทุกฟีเจอร์ใหม่ ให้คิดว่า "เปิด/ปิดได้จาก Settings" ตามหลัก Plug-in Ready (ข้อ 5.8 ใน PROJECT_PLAN.md) ยกเว้นเป็น core ที่จำเป็นเสมอ (เช่น POS พื้นฐาน)

## 5. กฎเฉพาะโมดูล License (สำคัญมาก — ห้ามพลาด)
- **ห้าม** เขียน Private Key (RSA) ไว้ใน `HotelPOS.Licensing` หรือ `HotelPOS.UI` เด็ดขาด — Private Key อยู่ใน `HotelPOS.LicenseAdminTool` เท่านั้น (ฝั่งลูกค้าเก็บแค่ Public Key สำหรับ "ตรวจสอบ" ลายเซ็น ไม่ใช่ "สร้าง")
- ทุกครั้งที่แก้โค้ด License ต้องเขียน Unit Test คู่กันเสมอ (กัน logic license พังโดยไม่รู้ตัว เพราะกระทบรายได้โดยตรง)
- ห้าม hardcode วันหมดอายุ/เงื่อนไขใดๆ ไว้ตรงๆ ในโค้ด ทุกอย่างต้องอ่านจากไฟล์ license ที่เซ็นลายเซ็นแล้วเท่านั้น
- Hardware ID ต้อง generate จากอย่างน้อย 2 แหล่ง (เช่น CPU ID + Disk Serial) แล้ว hash รวมกัน กันปลอมแปลงง่ายเกินไป
- ก่อน merge/ส่งมอบโค้ด License ทุกครั้ง ให้ทดสอบ: (1) เปิดเครื่องใหม่ = ได้ trial, (2) copy license.dat ไปอีกเครื่อง = ใช้ไม่ได้, (3) แก้ไฟล์ license.dat มือ = โปรแกรมปฏิเสธ, (4) หมดอายุ = ล็อกการใช้งานแต่ยังเปิดดูข้อมูลเก่าได้ (ห้าม lock ข้อมูลผู้ใช้)

## 6. กฎเฉพาะโมดูล Logger
- ใช้ logger ที่ตั้งค่าไว้ใน `HotelPOS.Logging` เท่านั้น ห้ามสร้าง Serilog instance ใหม่ในไฟล์อื่น
- ทุก log ต้องมี field ครบ: `timestamp, level, category, message, user_id, machine_id, module, correlation_id`
- Exception ทุกตัวต้อง log พร้อม stack trace เต็ม (ไม่ตัดทอน)
- ห้าม log ข้อมูลอ่อนไหว (รหัสผ่าน, เลขบัตรเครดิต) ลง log แม้จะ debug ก็ตาม

## 7. กฎเฉพาะโมดูล Printing
- ทุกเทมเพลตใบเสร็จ/เอกสาร ต้องดึงค่า ชื่อร้าน/โลโก้/ที่อยู่ จากตาราง `settings` เสมอ ห้าม hardcode
- ต้องรองรับอย่างน้อย 3 ขนาดกระดาษ: 58mm, 80mm, A4 — และเลือกเครื่องพิมพ์ได้จาก dropdown (`PrinterSettings.InstalledPrinters`)
- ทดสอบพิมพ์ด้วย "Print Preview" ก่อนพิมพ์จริงเสมอ (ลดกระดาษเสีย ช่วย debug ง่าย)

## 8. ลำดับการทำงานที่แนะนำ (ทำเป็น Phase ตาม Roadmap ใน PROJECT_PLAN.md ข้อ 8)
เมื่อผู้ใช้ขอให้ "เริ่มเขียนโค้ด" โดยไม่ระบุโมดูล ให้ถามก่อนว่าจะเริ่มจาก Phase ไหน
(ห้ามเขียนทั้งระบบทีเดียวในคำตอบเดียว เพราะจะยาวเกินไปและตรวจสอบคุณภาพยาก)
ลำดับที่แนะนำ: Setup → License Core → ห้องพัก/จอง → POS → พิมพ์เอกสาร → รายงาน/Backup → Polish → Packaging

## 9. ก่อนส่งมอบโค้ดทุกครั้ง (Definition of Done)
- [ ] Build ผ่านไม่มี warning ที่เป็น error-level
- [ ] มี try-catch + log ครบทุกจุดเสี่ยง
- [ ] ทดสอบบน .NET 8 (และถ้าเป็นไปได้ ทดสอบแบบ self-contained บนเครื่อง spec ต่ำ)
- [ ] ข้อความ UI เป็นภาษาไทยครบถ้วน ไม่มีคำอังกฤษหลงเหลือ (ยกเว้นศัพท์เทคนิคที่ไม่มีคำแปล)
- [ ] อัปเดต `PROJECT_PLAN.md` ถ้ามีการเปลี่ยนแปลง schema/ฟีเจอร์สำคัญ
