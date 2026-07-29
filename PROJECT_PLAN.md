# แผนแม่บทระบบ "Hotel POS" (ระบบขาย/บริหารห้องพัก แบบ One-Stop Service)

> เอกสารนี้ใช้เป็นพิมพ์เขียวหลักสำหรับพัฒนาโปรแกรม แจกจ่ายเชิงพาณิชย์ สำหรับธุรกิจขนาดเล็ก-กลาง
> (โฮมสเตย์ / รีสอร์ทเล็ก / โรงแรมขนาดกลาง ไม่เกินประมาณ 100-150 ห้อง)

---

## 1. ภาพรวมโปรเจค

| หัวข้อ | รายละเอียด |
|---|---|
| ชื่อระบบ (ชั่วคราว) | **HotelPOS TH** |
| กลุ่มเป้าหมาย | ธุรกิจที่พักขนาดเล็ก-กลาง ในไทย |
| แพลตฟอร์ม | Windows Desktop (7/8/10/11) ทำงานบนเครื่องเก่าได้ |
| โหมดใช้งาน | Offline-first, ทำงานได้แม้ไม่มีเน็ต, เชื่อมเน็ตเฉพาะตอน activate license |
| ภาษา UI | ไทย 100% (เผื่อ multi-language ในอนาคต) |
| รูปแบบขาย | ขาย License แบบ Perpetual/รายปี ผ่านตัวแทน/ทีมขายเอง |

---

## 2. Tech Stack ที่เลือก และเหตุผล

เนื่องจากโจทย์คือ **"ใช้งานได้แม้คอมรุ่นเก่า" + "ใช้งานง่าย" + "ต้องพิมพ์ใบเสร็จ/A4 ได้ยืดหยุ่น" + "ต้องทำระบบ License ป้องกันการคัดลอก"**
ทางเลือกที่เหมาะสมที่สุดคือ:

### ตัวเลือกหลัก (แนะนำ): **C# (.NET 8) + WinForms + SQLite**

เหตุผล:
- **เบา เร็ว รันบน Windows 7 ขึ้นไปได้จริง** (ใช้ .NET 8 self-contained หรือ .NET Framework 4.8 ถ้าต้องรองรับ Windows 7 แบบเป๊ะๆ)
- WinForms = UI แบบ native, โหลดไว, ไม่กิน RAM เหมือน Electron/Web-based app (ตัด React/Electron ออกเพราะกิน RAM สูง ไม่เหมาะกับคอมเก่า)
- **SQLite** = ฐานข้อมูลไฟล์เดียว ไม่ต้องติดตั้ง Server แยก, backup/restore ง่ายมาก (copy ไฟล์เดียวจบ)
- ระบบพิมพ์ของ .NET (`System.Drawing.Printing`) รองรับเครื่องพิมพ์ทุกชนิดอยู่แล้ว (Receipt/Slip ESC-POS ไปจนถึง Inkjet/Laser A4) แบบ "Universal Printer Dialog"
- คอมไพล์เป็น .exe ตัวเดียว ป้องกันการ copy ได้ง่ายกว่า script ภาษา interpreted (Python/Node)
- มี Obfuscator (เช่น ConfuserEx, .NET Reactor) ช่วยป้องกันการ decompile/crack license ได้ดี
- Ecosystem ไลบรารีไทยเยอะ (font, บาร์โค้ด, QR พร้อมเพย์, ESC/POS ภาษาไทย)

### ตัวเลือกรอง (ถ้าต้องการ Cross-platform ในอนาคต)
- **Python + PySide6 (Qt) + SQLite** — เขียนไวกว่า แต่ป้องกัน license/ป้องกันคัดลอกยากกว่า (ต้อง compile ด้วย Nuitka/PyInstaller + obfuscate เพิ่ม)

> **สรุป: ใช้ C# .NET 8 (WinForms) + SQLite เป็นหลัก** เอกสาร/Skill ด้านล่างอิงตามสแตกนี้ทั้งหมด

### Libraries ที่แนะนำ
| งาน | Library |
|---|---|
| ORM / DB Access | `Dapper` (เบา เร็ว ควบคุม SQL เองได้ ดีกับ SQLite) |
| Database | `Microsoft.Data.Sqlite` + SQLite file |
| พิมพ์เอกสาร/ใบเสร็จ | `System.Drawing.Printing` (built-in), เสริมด้วย `RDLC` หรือ custom PrintDocument สำหรับ A4 |
| พิมพ์ Slip 80mm/58mm | `ESC/POS` command builder เอง (ผ่าน RawPrinterHelper) รองรับ USB/Network printer |
| Logging | `Serilog` (เขียน log แบบ structured, rolling file, ระดับ log ชัดเจน) |
| Reporting/กราฟ | `LiveCharts2` หรือ `System.Windows.Forms.DataVisualization.Charting` (เบากว่า) |
| Excel Export | `ClosedXML` |
| PDF Export | `QuestPDF` (ฟรี, ทันสมัย, ทำใบเสร็จ/รายงาน PDF สวยงาม) |
| Encryption (License) | `System.Security.Cryptography` (AES + RSA signature) |
| Barcode/QR (PromptPay) | `QRCoder` |
| Auto-update (ทางเลือก) | `Squirrel.Windows` หรือ custom updater |

---

## 3. สถาปัตยกรรมระบบ (Layered Architecture)

```
HotelPOS.sln
├── HotelPOS.UI              (WinForms - หน้าจอทั้งหมด)
├── HotelPOS.Core             (Business Logic / Services)
├── HotelPOS.Data             (Repository + SQLite Access, Migration)
├── HotelPOS.Licensing        (ระบบ License แยกเป็นโมดูลอิสระ)
├── HotelPOS.Printing         (Engine พิมพ์ Receipt/A4 แบบ Universal)
├── HotelPOS.Logging          (Wrapper รอบ Serilog + Log Viewer)
├── HotelPOS.Common           (Models, DTO, Helper, Constants)
└── HotelPOS.LicenseAdminTool (โปรแกรมแยกต่างหาก สำหรับฝั่งผู้ขาย Gen Key)
```

หลักการ: **แยก License/Logging เป็นโมดูลอิสระ (plug-in style)** เพื่อให้ถอด/อัปเดต/ปรับปรุงได้โดยไม่กระทบส่วนอื่น และเผื่ออนาคตอยากทำเป็น DLL ปิด (ป้องกันการแกะดูโค้ดสำคัญ)

---

## 4. โครงสร้างฐานข้อมูล (หลัก ๆ)

### ตารางหลัก
- `rooms` — ห้องพัก (เลขห้อง, ประเภทห้อง, ชั้น, สถานะ: ว่าง/ไม่ว่าง/ทำความสะอาด/ซ่อมบำรุง)
- `room_types` — ประเภทห้อง + ราคา (รายวัน/รายชม./รายเดือน)
- `bookings` — การจอง (เช็คอิน, เช็คเอาท์, ลูกค้า, ห้อง, สถานะ)
- `customers` — ข้อมูลลูกค้า/สมาชิก (ชื่อ, เบอร์, เลขบัตร ปชช./พาสปอร์ต, ประวัติเข้าพัก)
- `folios` — บิลเปิดของแต่ละห้อง/ผู้เข้าพัก (รวมค่าห้อง+ค่าใช้จ่ายเสริม)
- `products` — สินค้า/บริการเสริม (มินิบาร์, ซักรีด, อาหาร, ค่าปรับ ฯลฯ)
- `product_categories`
- `sales` / `sale_items` — รายการขาย POS
- `payments` — การชำระเงิน (เงินสด/โอน/บัตร/พร้อมเพย์) รองรับหลายช่องทางต่อบิล
- `invoices` / `receipts` — เอกสารที่พิมพ์ออก (เก็บเลขที่เอกสารรันต่อเนื่อง)
- `users` — ผู้ใช้งานระบบ
- `roles` / `permissions` — สิทธิ์การเข้าถึงแยกตามเมนู/ฟีเจอร์
- `settings` — ตั้งค่าระบบทั้งหมด (key-value หรือ JSON)
- `audit_logs` — บันทึกการกระทำของผู้ใช้ (ใครทำอะไร เมื่อไหร่)
- `app_logs` — log ระบบ/error (อ้างอิงหัวข้อ Logger ด้านล่าง)
- `license_info` — ข้อมูล license ที่ผูกกับเครื่อง (เก็บแบบเข้ารหัส)
- `backup_history` — ประวัติการ backup/restore

> ทุกตารางมี `created_at`, `updated_at`, `created_by`, `is_deleted` (soft delete) เพื่อรองรับการดูประวัติย้อนหลัง

---

## 5. รายการฟีเจอร์แบบเต็ม (One-Stop Service)

### 5.1 การจัดการห้องพัก
- ผังห้องแบบภาพ (Room Grid/Map) เห็นสถานะสีตามห้อง (ว่าง/มีคน/ทำความสะอาด/ปิดซ่อม)
- เช็คอิน/เช็คเอาท์/ย้ายห้อง/ต่อวัน/Early check-in/Late check-out
- จองล่วงหน้า (ปฏิทินการจอง)
- รองรับราคาห้องแบบ รายวัน/รายชั่วโมง/รายเดือน + ราคาพิเศษ (โปรโมชั่น/ราคาสมาชิก)

### 5.2 POS ขายสินค้า/บริการเสริม
- ขายหน้าร้าน (มินิบาร์ ร้านค้า อาหารเครื่องดื่ม)
- เพิ่มรายการเข้า Folio ห้องพัก (รวมบิลตอนเช็คเอาท์ได้)
- ส่วนลด/โปรโมชั่น/แต้มสะสม (ทางเลือกเสริม)

### 5.3 บิล/ใบเสร็จ/เอกสาร
- ออกใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ/เต็มรูป
- พิมพ์ได้ทั้ง **Slip 58mm/80mm (Receipt Printer)** และ **A4 (Laser/Inkjet)** แบบเลือกเทมเพลตได้
- ตั้งค่าโลโก้ร้าน/ชื่อร้าน/ที่อยู่/เลขผู้เสียภาษี/เลขที่เอกสารเริ่มต้น
- Export เป็น PDF ส่งอีเมล/ไลน์ได้

### 5.4 Backup / Restore / Reset
- Backup อัตโนมัติ (รายวัน/ตั้งเวลา) + Backup ด้วยมือ
- Restore จากไฟล์ backup (มี checksum ป้องกันไฟล์เสีย)
- Reset ข้อมูล (แยกโหมด: reset ข้อมูลขาย vs ล้างทั้งหมดโรงงาน) พร้อมยืนยันรหัสผ่านผู้ดูแล
- เก็บ backup ลง external drive / Google Drive (ทางเลือกเสริมภายหลัง)

### 5.5 รายงาน/ดูข้อมูลย้อนหลัง
- รายงานยอดขายรายวัน/เดือน/ปี, แยกตามพนักงาน/ห้อง/หมวดสินค้า
- รายงานอัตราการเข้าพัก (Occupancy Rate), รายได้เฉลี่ยต่อห้อง (ADR/RevPAR)
- ประวัติลูกค้า, Log การเข้าใช้งาน, Audit Trail
- Export Excel/PDF ได้ทุกรายงาน

### 5.6 ตั้งค่าระบบ (ละเอียดยิบ)
- ข้อมูลร้าน: ชื่อ, โลโก้, ที่อยู่, เบอร์โทร, เลขผู้เสียภาษี
- ตั้งค่าห้อง/ราคา/ประเภทห้อง
- ตั้งค่าเครื่องพิมพ์ (เลือกเครื่อง, ขนาดกระดาษ, ฟอนต์, เทมเพลตใบเสร็จ)
- ตั้งค่าผู้ใช้/สิทธิ์การเข้าถึง (Role-based)
- ตั้งค่าภาษี/ค่าธรรมเนียม/ส่วนลด
- ตั้งค่าภาษา (เผื่ออนาคต), ธีมสี, ขนาดตัวอักษร (รองรับจอเก่า/สายตาไม่ดี)

### 5.7 ระบบ Backup อัตโนมัติ + แจ้งเตือน License ใกล้หมดอายุ

### 5.8 โมดูลเสริมที่เพิ่ม/ลดได้เอง (Plug-in Ready)
ออกแบบให้แต่ละฟีเจอร์ใหญ่ (POS, จองห้อง, รายงาน, License, Printing) เป็น "โมดูล" แยกจากกันชัดเจน
เพื่อให้ **เพิ่ม/ปิดการใช้งานฟีเจอร์ได้ผ่านหน้า Settings** โดยไม่ต้องแก้โค้ดหลัก เช่น เปิด/ปิดโมดูลจองห้อง, โมดูลสมาชิก/แต้มสะสม, โมดูลซักรีด ฯลฯ

---

## 6. ระบบ License แบบละเอียด (USB Hardware Dongle 100%)

### 6.1 แนวคิดหลัก
- License อนุมัติสิทธิ์การใช้งานถาวร/รายปีผ่าน **USB Hardware Dongle (`dongle.key`)**
- ดึง **Physical Hardware Serial** ระดับชิป USB คอนโทรลเลอร์ (`Win32_DiskDrive WHERE InterfaceType='USB'`) ผูกกับดิจิทัลซิกเนเจอร์ RSA-2048 **ก๊อปปี้ไฟล์ข้าม Flash Drive ไม่ได้ 100%**
- ใช้ **App Serial Watermark (`app.watermark`)** ลายน้ำประจำตัวชุดโปรแกรม `.exe` ป้องกันการนำ USB Dongle ของลูกค้า A ไปใช้กับแอปของลูกค้า B
- หากไม่มี USB Dongle เสียบอยู่ จะสลับเข้าโหมด **Trial 30 วัน Anti-Reset** (ฝังวันที่ใน Registry + Hidden file + SQLite DB ยึดวันที่เก่าที่สุดนับถอยหลังต่อเสมอ)

### 6.2 ข้อมูลใน Dongle Key (`dongle.key`)
```json
{
  "customer_name": "ชื่อร้าน/ลูกค้า",
  "usb_hardware_id": "SHA256-ของ-Physical-USB-Serial",
  "app_serial": "APP-2026-CLIENT-A",
  "license_type": "TRIAL | STANDARD | LIFETIME",
  "issue_date": "2026-07-26",
  "expire_date": null,            // null = ถาวร
  "max_rooms": 50,                // จำกัดจำนวนห้องตามแพ็กเกจ (option)
  "features": ["POS","BOOKING","REPORT"],
  "signature": "RSA-2048-SHA256-DIGITAL-SIGNATURE"
}
```

### 6.3 เครื่องมือฝั่งผู้ขาย (License Admin Tool 100% USB Dongle Center)
โปรแกรม `HotelPOS.LicenseAdminTool.exe` สำหรับทีมขาย/ผู้พัฒนา:
- Auto-detect USB Flash Drive ที่เสียบอยู่กับคอมพิวเตอร์
- Gen & Sign ทั้ง `dongle.key` เขียนลง USB Drive และออกไฟล์ `app.watermark` ในคลิกเดียว
- ปุ่ม **"⚡ ทดสอบปลดล็อก"** สำหรับสอบทานสิทธิ์ USB Dongle บนเครื่องนักพัฒนาทันที

---

## 7. ระบบ Logger แบบละเอียด (ตามบัคง่าย)

### 7.1 หลักการออกแบบ
ใช้ **Serilog** เขียน log แบบ **Structured Logging (JSON)** แยกเป็นไฟล์ตามวัน + แยกตามระดับความรุนแรง

### 7.2 ระดับ Log (Log Levels)
| Level | ใช้เมื่อ |
|---|---|
| `TRACE` | รายละเอียดยิบทุก step (debug ลึกสุด, เปิดเฉพาะตอน dev/สืบบัค) |
| `DEBUG` | ค่าตัวแปรสำคัญ, การเรียก function หลัก |
| `INFO` | เหตุการณ์ปกติ (เช็คอิน, ขายของสำเร็จ, พิมพ์บิล) |
| `WARNING` | สิ่งผิดปกติแต่ยังทำงานต่อได้ (เครื่องพิมพ์ตอบช้า, ค่า config หาย ใช้ default) |
| `ERROR` | ทำงานล้มเหลวเฉพาะจุด (พิมพ์บิลไม่ได้, บันทึกข้อมูลไม่ผ่าน) |
| `FATAL` | โปรแกรม crash / DB เสียหาย |

### 7.3 หมวดหมู่ Log (Category) — เพื่อกรองหาได้ง่าย
`UI`, `DATABASE`, `PRINTING`, `LICENSE`, `BOOKING`, `POS`, `BACKUP`, `AUTH`, `SYSTEM`

### 7.4 รูปแบบการเก็บ
- ไฟล์: `logs/YYYY-MM-DD/app-log.json` (rolling รายวัน, เก็บย้อนหลังตั้งค่าได้ เช่น 90 วัน แล้วลบอัตโนมัติ)
- แต่ละบรรทัด log เก็บ: `timestamp, level, category, message, exception_stacktrace, user_id, machine_id, module, method_name, correlation_id`
- `correlation_id` — เลขอ้างอิงเดียวกันตลอด flow หนึ่ง action (เช่น กด "พิมพ์ใบเสร็จ" 1 ครั้ง = 1 correlation_id) ช่วยไล่ดู log ของเหตุการณ์เดียวได้ทั้งหมดในคลิกเดียว

### 7.5 หน้าจอ Log Viewer (ในตัวโปรแกรม)
- ค้นหา/กรอง log ตาม วันที่/ระดับ/หมวดหมู่/คำค้น ได้จากหน้า UI โดยตรง (ไม่ต้องเปิดไฟล์เอง)
- ปุ่ม **"Export Log ส่งให้ทีมซัพพอร์ต"** (zip ไฟล์ log ช่วงที่เลือก) ลูกค้ากดส่งให้ทีม support ได้ทันทีเวลาเจอปัญหา
- แจ้งเตือนอัตโนมัติ (popup เล็กๆ มุมจอ) เมื่อเกิด `ERROR`/`FATAL` พร้อมปุ่ม "ดูรายละเอียด/ส่งรายงาน"

### 7.6 Global Exception Handler
ดัก Exception ทุกจุดของโปรแกรม (`AppDomain.UnhandledException`, `Application.ThreadException`, try-catch รายฟังก์ชันสำคัญ) ไม่ให้โปรแกรม crash เงียบๆ — บันทึก log + แสดงข้อความที่เข้าใจง่ายให้ผู้ใช้ พร้อมรหัส error อ้างอิง

---

## 8. Roadmap การพัฒนา (แบ่งเฟส)

| Phase | เนื้อหา | สถานะ |
|---|---|---|
| **0. Setup** | ตั้งโปรเจค, โครงสร้าง Solution, DB Schema, Logging infra | ✅ เสร็จสมบูรณ์ |
| **1. Core License** | ระบบ USB Hardware Dongle + App Serial Watermark + Trial 30 วัน Anti-Reset + License Admin Tool | ✅ เสร็จสมบูรณ์ (v1.0.0) |
| **2. ห้องพัก/จอง** | Room Grid, เช็คอิน/เช็คเอาท์, จองล่วงหน้า | ✅ เสร็จสมบูรณ์ |
| **3. POS/ขายของ** | ขายสินค้าเสริม, Folio, ส่วนลด | ✅ เสร็จสมบูรณ์ |
| **4. บิล/พิมพ์เอกสาร** | Engine พิมพ์ Receipt + A4, เทมเพลต, ตั้งค่าโลโก้/ร้าน | ✅ เสร็จสมบูรณ์ |
| **5. รายงาน/Backup** | รายงานทั้งหมด, Backup/Restore/Reset | ⏳ กำลังพัฒนา |
| **6. Polish** | UI/UX, สิทธิ์ผู้ใช้ (Role), Log Viewer, ทดสอบระบบทั้งหมด | ⏳ กำลังพัฒนา |
| **7. Packaging** | ทำ Installer (Inno Setup), Obfuscate, ทดสอบบนเครื่องเก่าจริง | ⏳ กำลังพัฒนา |

---

## 9. หมายเหตุเรื่องการติดตั้งจริง
- ใช้ **Inno Setup** ทำตัวติดตั้ง (.exe) แบบมืออาชีพ มีโลโก้ ทางลัดหน้าจอ ถอนการติดตั้งได้
- แนบ .NET 8 Desktop Runtime ไปกับตัวติดตั้ง (เผื่อเครื่องลูกค้าไม่มี) หรือ build แบบ self-contained (ไฟล์ใหญ่ขึ้นแต่ไม่ต้องพึ่ง runtime)
- ทดสอบบน Windows 7 SP1 / RAM 2-4GB จริงก่อนขาย

---

## 10. ประวัติการบันทึก Progress (Change Log History — v1.0.0)

| วันที่/เวลา | เวอร์ชัน | รายละเอียดการบันทึกความคืบหน้า (Progress Log) |
|---|---|---|
| **2026-07-29** | **v1.0.0** | **พัฒนาระบบแบ่งหน้าแสดงผลตารางเพื่อป้องกัน UI ค้าง, แก้ปัญหาส่งออก Excel เลขเพี้ยน, เพิ่มประวัติค่าน้ำไฟเชิงลึกแยกรายลูกค้า, และแก้ไขงานพิมพ์ A4 ทับซ้อน:**<br>- พัฒนาและติดตั้งแผงควบคุมการแบ่งหน้าส่วนกลาง `GridPaginationPanel` ใน 5 ตารางหลักเพื่อลดการสะดุดและหน่วงหน้าจอ ได้แก่: ตารางข้อมูลผู้เข้าพัก, ตารางการจอง, ประวัติบิล POS, ประวัติค่าน้ำไฟ และประวัติ Audit Logs ระบบ<br>- ปรับปรุงสูตรแปลงฟิลด์ส่งออกข้อมูล CSV (`"=""{value}"""`) เพื่อล็อกเบอร์โทร, เลขบัตรประชาชน, รหัสสินค้า, และรหัสบิลไม่ให้ Excel แปลงค่าเป็นเลขยกกำลังวิทยาศาสตร์ (Scientific Notation) หรือตัดเลขศูนย์ด้านหน้าทิ้ง<br>- เพิ่มแท็บค่าน้ำ/ค่าไฟย้อนหลังในประวัติลูกค้า เชื่อมโยงดึงบิลห้องตามช่วงวันเข้าพักอัตโนมัติ พร้อมระบบ Double-Click เพื่อเปิด Print Preview บิลห้องพัก, บิล POS หรือบิลน้ำไฟใบจริงโดยตรง<br>- ปรับแต่งการวาดส่วนหัวพิมพ์ A4 ด้วย `StringFormat` จัดแนวขวา (Far Alignment) และลดขนาดฟอนต์ลายเซ็นเหลือ 9.5F เพื่อแก้ไขปัญหาตัวหนังสือทับซ้อนและล้นกล่องเซ็นชื่อ |
| **2026-07-29** | **v1.0.0** | **พัฒนาระบบค่าน้ำ-ค่าไฟแบบรวมศูนย์บนผังห้องพัก (Room Grid Integration) และระบบการพิมพ์แยกแบบสลิป (Receipt 80mm) และ A4:**<br>- รวมศูนย์การเปิด/บันทึกค่าน้ำ-ค่าไฟไว้ที่หน้า **ผังห้องพัก (Room Grid)** โดยตรง ผ่านเมนูปริบทคลิกขวาที่ห้องพัก และปุ่ม `กรอกค่าน้ำ-ค่าไฟ` ในฟอร์มคืนห้องพัก/เช็คเอาท์ `CheckOutForm.cs`<br>- เพิ่มปุ่มพิมพ์บิลแยก 2 รูปแบบชัดเจนที่หน้าเช็คเอาท์: ปุ่ม **พิมพ์สลิป (Receipt 80mm)** และปุ่ม **พิมพ์ใบแจ้งหนี้ (A4)** รองรับทั้งเครื่องพิมพ์ความร้อนและเครื่องพิมพ์กระดาษ A4<br>- รองรับโหมดคิดค่าไฟฟ้า 2 รูปแบบ: แบบตามมิเตอร์ (`METER`) และแบบเหมาจ่ายรายเดือน (`FLAT`) พร้อมตั้งค่าอัตราใน `UtilityRateSettingsForm.cs`<br>- ล็อกคอลัมน์ที่ระบบคำนวณอัตโนมัติในตาราง (ไฟ-ก่อน, หน่วยไฟ, ค่าไฟ, น้ำ-ก่อน, หน่วยน้ำ, ค่าน้ำ, ยอดสุทธิ) เป็น `ReadOnly` ป้องกันการแก้ไข<br>- ถอดสัญลักษณ์อิโมจิ (Emoji) ทั้งหมดออกจากหน้าต่าง UI ของโปรแกรมตามข้อกำหนดอินเทอร์เฟซ |
| **2026-07-29** | **v1.0.0** | **พัฒนาระบบ Pop-up Dialog กรอกและคำนวณค่าน้ำ-ค่าไฟอัตโนมัติรายห้อง, ปรับปรุงรูปแบบใบเสร็จ, แก้ไขปุ่มคิดเงินมินิบาร์/POS, และเพิ่มระบบ Export/Import สต็อกสินค้า (Products.csv):**<br>- พัฒนา Pop-up Dialog `MeterReadingInputDialog.cs` เพิ่มความสะดวกในการจดมิเตอร์เมื่อดับเบิลคลิกแถวห้องพักในตารางหรือกดปุ่ม `กรอก/แก้ไข`<br>- ระบบดึงเลขมิเตอร์สิ้นสุดของเดือนก่อนหน้ามาแสดงในช่อง `มิเตอร์ก่อนหน้า (ล็อก)` ให้โดยผู้ใช้คีย์เฉพาะเลขมิเตอร์ล่าสุดของเดือนปัจจุบัน<br>- ระบบหักลบ คำนวณหน่วยที่ใช้ และคำนวณยอดเงินรวม (ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าส่วนกลาง + ค่าขยะ + ค่าจิปาถะ - ส่วนลด) แบบเรียลไทม์ขณะคีย์ตัวเลข พร้อมบันทึกเข้าฐานข้อมูล SQLite อัตโนมัติ<br>- ปรับปรุงฟังก์ชัน `DrawLeftRight()` และ `RenderA4Layout()` ใน `ReceiptInvoicePrinter.cs` และ `UtilityInvoicePrinter.cs` ป้องกันปัญหาข้อความภาษาไทยซ้อนทับกัน<br>- ปรับโครงสร้างพาเนลตะกร้าสินค้าใน `POSControl.cs` แยกพาเนลชำระเงินขอบล่าง (`Dock = DockStyle.Bottom`) พร้อมปุ่ม **คิดเงิน / ชำระเงิน (F10)** เด่นชัด<br>- เพิ่มระบบ Export / Import สินค้าและสต็อก (`Products.csv`) ใน `ExportImportService.cs`, `SystemBackupControl.cs`, และหน้าจัดการสต็อก POS |
| **2026-07-27** | **v1.0.0** | **พัฒนาระบบเครื่องพิมพ์ใบเสร็จ (Thermal Printer), Rebrand โปรแกรมเป็น PSoft Rest & Rent Manager, และระบบบริการเสริม & ร้านค้า (POS):**<br>- พัฒนาการจัดหน้าและพิมพ์ใบเสร็จขนาด 58mm / 80mm เพิ่มเติมจากบิล A4 เดิมใน `ReceiptInvoicePrinter.cs` และ `UtilityInvoicePrinter.cs`<br>- พัฒนาระบบแปลงตัวอักษรภาษาไทยเป็นรูปภาพ (Thai Rasterization) ป้องกันปัญหาฟอนต์ภาษาไทยของเครื่องพิมพ์จีนราคาประหยัด<br>- ดำเนินการ Rebrand ชื่อระบบจาก HotelPOS เป็น **PSoft Rest & Rent Manager** ครอบคลุม: ชื่อหน้าต่างโปรแกรม, ที่อยู่จัดเก็บฐานข้อมูล/Log ใต้ AppData, ค่าคีย์ Registry คลาสทดสอบ และปรับชื่อไฟล์ Binary เอาต์พุตเป็น `PSoftRestRentManager.exe` และ `PSoftRestRentGenerator.exe`<br>- พัฒนาระบบร้านค้าและบริการเสริม (POS Shop Front) แบบสองคอลัมน์ พร้อมระบบจัดการสต็อก/ประเภทสินค้าในตัว เชื่อมต่อ Folio ชาร์จเข้าบัญชีห้องพัก และพิมพ์ใบเสร็จอย่างย่อผ่านเครื่องพิมพ์ความร้อนแบบเรียลไทม์ |
| **2026-07-27** | **v1.0.0** | **เริ่มต้นโปรเจค & พัฒนาระบบ License ทั้งหมดเป็น USB Hardware Dongle 100%:**<br>- พัฒนาคลาส `UsbDongleManager.cs` ดึง Physical Hardware Serial ระดับชิป USB คอนโทรลเลอร์ (`Win32_DiskDrive WHERE InterfaceType='USB'`) ป้องกันการก๊อปปี้ไฟล์ข้าม Flash Drive 100%<br>- พัฒนาคลาส `AppWatermarkManager.cs` จัดการรหัสประจำตัวโปรแกรม (`app.watermark`) ป้องกันการสลับ Dongle ข้ามแอป<br>- พัฒนาคลาส `RevocationManager.cs` จัดการไฟล์บัญชีดำระงับสิทธิ์ (`revoked.dat`) ด้วย RSA Digital Signature<br>- ปรับปรุง `LicenseValidator.cs` และ `LicenseManager.cs` ให้สแกนหา USB Dongle เป็นอันดับแรก หากไม่พบจะสลับเข้าโหมด Trial 30 วัน Anti-Reset (ยึดวันที่เก่าที่สุดใน 3 แหล่ง)<br>- ปรับปรุง `HotelPOS.LicenseAdminTool` ให้รองรับการ Auto-detect USB Drive, Gen & Sign `dongle.key`, Gen `app.watermark` และปุ่มปลดล็อกทดสอบบนเครื่องนักพัฒนา<br>- เพิ่มชุดทดสอบ Unit Tests ผ่าน 25/25 เคส (Passed 100%) และสร้างคู่มือทดสอบ `Docs/USB_DONGLE_TESTING_MANUAL.md` |


