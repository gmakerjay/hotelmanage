# คู่มือการตั้งค่าระบบ Logger และการตรวจสอบประวัติการทำงาน (Logger & Audit Trail Guide)
**โปรแกรมจัดการห้องพัก PSOFT — เวอร์ชัน 1.0.0**

---

## 1. 📌 ภาพรวมสถาปัตยกรรมการเก็บ Log (Logging Architecture)

ระบบ Logger ของ **โปรแกรมจัดการห้องพัก PSOFT** ออกแบบตามมาตรฐานระดับองค์กร โดยใช้ **Serilog Engine** ร่วมกับโครงสร้างแบบ **Multi-Sink Categorized File Architecture** เพื่อแก้ปัญหาไฟล์ Log บวม รวมกระจุกอยู่ไฟล์เดียว หรือเปิดอ่านช้าเมื่อโปรแกรมทำงานเป็นระยะเวลานาน

---

## 2. 📂 โครงสร้างโฟลเดอร์และการแยกหมวดหมู่ไฟล์ Log

ไฟล์ Log ทั้งหมดจะถูกแยกเก็บเป็นหมวดหมู่อย่างเป็นระเบียบภายใต้โฟลเดอร์หลัก:
`%AppData%\HotelPOS\logs\` (หรือ `C:\Users\<Username>\AppData\Roaming\HotelPOS\logs\`)

```
%AppData%\HotelPOS\logs\
  ├── errors/          --> error_YYYY-MM-DD.txt
  ├── system/          --> system_YYYY-MM-DD.txt
  ├── database/        --> db_YYYY-MM-DD.txt
  ├── audit/           --> audit_YYYY-MM-DD.txt
  └── json/            --> app_YYYY-MM-DD.json
```

### 📋 รายละเอียดแต่ละโฟลเดอร์

| ชื่อโฟลเดอร์ | ชนิดไฟล์ | วัตถุประสงค์และการใช้งาน |
| :--- | :--- | :--- |
| **`errors/`** | `error_YYYY-MM-DD.txt` | **เน้นเก็บ Error & Fatal Exception เท่านั้น** เป็นไฟล์สำคัญที่สุดที่ทีมซัพพอร์ตเปิดดูทันทีเมื่อลูกค้าแจ้งปัญหา อ่านง่ายด้วย Plain-Text |
| **`system/`** | `system_YYYY-MM-DD.txt` | เก็บการทำงานทั่วไปของโปรแกรม การเปิดหน้าจอ การกดปุ่ม และกระบวนการของระบบ |
| **`database/`** | `db_YYYY-MM-DD.txt` | เก็บการทำงานและคิวรีฐานข้อมูล SQLite (Database Transactions & Migrations) |
| **`audit/`** | `audit_YYYY-MM-DD.txt` | เก็บ Audit Trail กิจกรรมสำคัญของผู้ใช้ (เช็คอิน, เช็คเอาท์, พิมพ์ใบเสร็จ, ปรับปรุงราคา) |
| **`json/`** | `app_YYYY-MM-DD.json` | เก็บ Structured Log รูปแบบ Compact JSON เหมาะสำหรับการนำเข้า Log Analyzer / ELK / Seq เชิงลึก |

---

## 3. 🛡️ กลยุทธ์การป้องกันไฟล์บวมและการหมุนเวียนไฟล์ (Anti-Bloat & Log Rotation Policy)

เพื่อป้องกันไม่ให้ไฟล์ Log ใช้พื้นที่ดิสก์มากเกินไป หรือมีขนาดใหญ่จนโปรแกรมเปิดอ่านช้า ระบบได้รับการตั้งค่ากลไกป้องกัน 3 ระดับ:

1. **การหมุนเวียนไฟล์รายวัน (Daily Rolling)**:
   * ไฟล์จะตัดแยกใหม่ทุกเที่ยงคืนโดยอัตโนมัติ ตามรูปแบบ `YYYY-MM-DD`
2. **การจำกัดขนาดไฟล์สูงสุด 5 MB (File Size Limit & Rolling)**:
   * ทุกไฟล์ในโฟลเดอร์ `.txt` ถูกจำกัดขนาดสูงสุดไม่เกิน **5 MB (5,242,880 Bytes)**
   * หากมีการเขียน Log ในวันเดียวกันเกิน 5 MB ระบบจะตัดขึ้นไฟล์ใหม่เป็น `error_2026-07-26_001.txt`, `error_2026-07-26_002.txt` ทันที ทำให้เปิดอ่านด้วย Notepad ได้รวดเร็ว ไม่ค้าง
3. **การลบไฟล์เก่าอัตโนมัติ (Retention Policy)**:
   * ระบบตั้งค่าเก็บย้อนหลังสูงสุด **90 วัน** (Retained File Count Limit) เมื่อพ้นกำหนด 90 วัน ไฟล์ Log ที่เก่าที่สุดจะถูกลบออกจากดิสก์โดยอัตโนมัติ

---

## 4. 🔍 รูปแบบโครงสร้าง Log & Correlation ID เพื่อการ Audit

ทุกบรรทัดในไฟล์ Log จะถูกจัดรูปแบบให้มีข้อมูลครบถ้วนสำหรับสืบหาต้นตอของปัญหา:

### 📝 ตัวอย่างรูปแบบบรรทัด Log (`outputTemplate`)

```text
[2026-07-26 14:58:00.123 +07:00] [ERR] [System] [CorrId: 4f8a2b91c0e34a6fb7d1e8a9021b3c4d] [User: admin] [Machine: DESKTOP-SERVER1] เกิดข้อผิดพลาดในการเช็คเอาท์ห้อง 101
System.InvalidOperationException: ไม่พบข้อมูล Folio ของการจองหมายเลข 128
   at HotelPOS.Core.Services.BookingService.CheckOutAsync(...) in C:\HotelPOS\src\HotelPOS.Core\Services\BookingService.cs:line 215
```

### 🧩 องค์ประกอบสำคัญใน Log
* **`Timestamp`**: วันเวลาและเขตเวลาอย่างละเอียดระดับมิลลิวินาที
* **`Level`**: ระดับความสำคัญ (`VER`, `DBG`, `INF`, `WRN`, `ERR`, `FTL`)
* **`Category`**: หมวดหมู่โมดูล (`System`, `Database`, `Audit`, `Licensing`)
* **`CorrId` (Correlation ID)**: รหัสอ้างอิงธุรกรรม (Guid แบบสุ่ม 32 ตัวอักษร) ช่วยให้ติดตามเส้นทางการทำงานของการกดปุ่ม 1 ครั้งข้ามทุก Layer ตั้งแต่ UI -> Service -> Database
* **`User`**: ชื่อบัญชีผู้ใช้งานที่ทำรายการขณะนั้น (`admin` หรือ `UserId`)
* **`Machine`**: ชื่อเครื่องคอมพิวเตอร์ที่รันโปรแกรม

---

## 5. 🛠️ คู่มือสำหรับทีมซัพพอร์ตเมื่อลูกค้าแจ้งปัญหา (Troubleshooting Steps)

เมื่อลูกค้าแจ้งว่าโปรแกรมเกิดข้อผิดพลาด ให้ทำตามขั้นตอนดังนี้:

1. **ขอไฟล์ Log จากเครื่องลูกค้า**:
   * เปิด Windows Explorer และพิมพ์ `%AppData%\HotelPOS\logs\errors\` ในช่อง Address bar
   * คัดเลือกไฟล์ `error_YYYY-MM-DD.txt` ประจำวันที่เกิดปัญหา
2. **ค้นหาข้อผิดพลาดตามเวลา หรือ Correlation ID**:
   * เปิดไฟล์ด้วย Notepad หรือ Text Editor
   * ค้นหาคำว่า `[ERR]` หรือ `[FTL]` หรือค้นหาตาม `CorrId` ที่ได้จากหน้าจอแสดงข้อผิดพลาด
3. **วิเคราะห์ Stack Trace**:
   * ตรวจดูบรรทัด Exception, ชื่อคลาส และ Line Number เพื่อระบุสาเหตุที่แท้จริง (เช่น SQLite Constraint, Network Disconnect, Printer Unavailable)

---

*เอกสารฉบับนี้เป็นส่วนหนึ่งของระบบกำกับดูแลคุณภาพซอฟต์แวร์ โปรแกรมจัดการห้องพัก PSOFT v1.0.0*
