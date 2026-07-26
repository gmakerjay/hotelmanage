# ภาพรวมสถาปัตยกรรมและการตั้งค่าระบบ (HotelPOS TH System Summary)

> **เวอร์ชันระบบ**: `1.0.0`  
> **แพลตฟอร์ม**: Windows Desktop (.NET 8 WinForms + SQLite)  
> **วันอัปเดตล่าสุด**: 26 กรกฎาคม 2026

---

## 1. ข้อมูลการเข้าสู่ระบบเริ่มต้น (Default Admin Credentials)

โปรแกรมกำหนดระบบยืนยันตัวตนก่อนเข้าใช้งาน (Authentication) ไว้ดังนี้:

| รายการ | ค่าเริ่มต้น (Default Value) | หมายเหตุ |
|---|---|---|
| **ชื่อผู้ใช้ (Username)** | `admin` | ใส่คาไว้เป็นค่าเริ่มต้น |
| **รหัสผ่าน (Password)** | `psoft123` | **ไม่ใส่คาไว้** (ผู้ใช้ต้องกรอกรหัสผ่านเอง) |

---

## 2. การออกแบบแถบนำทางไซด์บาร์สีดำพรีเมียม (Modern Dark Left Sidebar Navigation)

โปรแกรมได้รับการปรับโฉมส่วนนำทางหลักให้เป็น **แถบไซด์บาร์แนวด้านซ้าย (Left Dark Sidebar)** สไตล์ SaaS Dashboard ระดับหรู:
* **โทนสีแถบไซด์บาร์ (Color Palette)**: สีดำ Slate/Charcoal เข้มพรีเมียม (`#14141E`)
* **ตัวหนังสือ**: สีขาวคมชัด (`#FFFFFF`) สำหรับรายการที่เลือกใช้งาน และสีเทา Slate (`#CBD5E1`) สำหรับรายการทั่วไป
* **แถบเน้นรายการที่เลือก (Active State)**: แถบสีน้ำเงินสด Electric Blue (`#2563EB`) ตัวหนังสือตัวหนาชัดเจน
* **ปราศจากอิโมจิ (Clean Minimalist Typography)**: ลบอิโมจิทั้งหมดออกเพื่อความสะอาด ตาเป็นระเบียบ และดูเป็นมืออาชีพ
* **ส่วนล่างแถบไซด์บาร์ (Sidebar Footer)**: แสดงข้อมูลผู้ใช้ `admin (ผู้ดูแลระบบ)` พร้อมปุ่ม **`ออกจากระบบ`** สีแดงสำหรับ Logout กลับไปยังหน้าล็อกอิน

---

## 3. สรุปโมดูลการทำงานหลัก

### 3.1 ระบบยืนยันตัวตน & ลิขสิทธิ์ (Authentication & Licensing)
* **หน้าล็อกอิน (`LoginForm`)**: เด้งถามหาผู้ใช้งานและรหัสผ่าน `admin / psoft123` เสมอเมื่อเปิดโปรแกรม โดยช่องรหัสผ่านเว้นว่างไว้ให้ผู้ใช้ป้อนเองเพื่อความปลอดภัย
* **ตรวจสอบ Hardware ID**: ลิขสิทธิ์ผูกกับเครื่องด้วย RSA Signature (Public Key ฝั่ง Client)

### 3.2 ผังห้องพักสไตล์ POS ร้านอาหาร (`RoomGridControl`)
* แสดงผังห้องพักรูปแบบ Visual Cards/Tiles (230x175 px) สไตล์ Restaurant POS Floor Plan
* แยกสีประจำสถานะ:
  * **ว่าง (Available)**: ปุ่ม Quick Check-In & จองล่วงหน้า
  * **มีผู้เข้าพัก (Occupied)**: ปุ่ม Quick Check-Out / คืนห้อง
  * **รอทำความสะอาด (Cleaning)**: ปุ่ม Quick Cleaned
  * **จองล่วงหน้า (Reserved)**: ปุ่ม Quick Check-In จอง
  * **ปิดซ่อมบำรุง (Maintenance)**: ปุ่ม Quick Enable
* แถบตัวกรองสถานะและชั้นสไตล์ POS Tab Buttons (ลบอิโมจิทั้งหมด)

### 3.3 ระบบตั้งค่าห้องพัก & ประเภทห้อง (`RoomManagementControl`)
* **ปุ่มกดจัดการในตาราง (Inline Action Buttons)**: เพิ่มปุ่ม `แก้ไข` และ `ลบ` ในทุกแถวของ DataGridView
* **ระบบป้องกันความปลอดภัยในการลบห้องพัก (Safe Room Deletion)**:
  * หากห้องพักเพิ่งสร้างใหม่และยังไม่มีประวัติการจอง ระบบจะลบข้อมูลออกจากฐานข้อมูลโดยตรง (Hard Delete)
  * หากห้องพักมีประวัติการจองแล้ว ระบบจะทำการซ่อนห้องพักอย่างปลอดภัย (Soft Delete - `is_active = 0`) ป้องกันข้อผิดพลาด `SQLite Error 19: FOREIGN KEY constraint failed` เพื่อให้ประวัติการเงินและการจองในอดีตสมบูรณ์ 100%

---

## 4. สถาปัตยกรรมการจัดเก็บไฟล์ Log & Audit Trail (`AppLogger.cs`)

ระบบ Logger ได้รับการออกแบบให้จัดเก็บไฟล์ `.txt` แยกหมวดหมู่ภายใต้ `%AppData%\HotelPOS\logs\` เพื่อความเรียบร้อย ไม่กระจุก และป้องกันไฟล์บวม:

* **`logs/errors/error_YYYY-MM-DD.txt`**: เก็บเฉพาะ Error & Fatal Exception อ่านง่ายด้วย Notepad ไม่เกะกะ
* **`logs/system/system_YYYY-MM-DD.txt`**: เก็บการทำงานระบบทั่วไป
* **`logs/database/db_YYYY-MM-DD.txt`**: เก็บการทำงานฐานข้อมูล SQLite
* **`logs/audit/audit_YYYY-MM-DD.txt`**: เก็บการทำรายการสำคัญของผู้ใช้
* **`logs/json/app_YYYY-MM-DD.json`**: เก็บ Compact JSON คุณภาพสูงสำหรับ Log Analytics
* **กลยุทธ์ป้องกันไฟล์บวม**: ตัดไฟล์ใหม่เมื่อเกิน **5 MB** ต่อไฟล์ (Roll on File Size Limit) และลบไฟล์เก่าเกิน **90 วัน** อัตโนมัติ

ดูรายละเอียดทั้งหมดในเอกสาร: [Docs/LOGGER_AND_AUDIT_GUIDE.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/LOGGER_AND_AUDIT_GUIDE.md)

---

## 5. ระบบตั้งค่า Backend & ออกใบเสร็จรับเงิน (`SystemSettingsControl.cs`)

* **ชื่อโปรแกรมอย่างเป็นทางการ**: **โปรแกรมจัดการห้องพัก PSOFT**
* **ตั้งค่าระบบ Backend ครบวงจร**: จัดการข้อมูลร้าน, เลขผู้เสียภาษี, ข้อความหัวบิล/ท้ายบิล, เครื่องพิมพ์ Windows, ขนาดกระดาษ (A4, 80mm, 58mm), มัดจำเริ่มต้น, VAT %
* **Auto-Resize โลโก้และ QR Code**: ย่อ/ขยายรูปภาพโลโก้และรูป PromptPay QR Code ให้อยู่ในสัดส่วนกระดาษที่ถูกต้องอัตโนมัติก่อนพิมพ์

---

## 6. บันทึกประวัติการเปลี่ยนแปลง (v1.0.0 Changelog)

รายละเอียดการพัฒนาทั้งหมดบันทึกอยู่ในไฟล์ `v1.0.0` ตามกฎการควบคุมเวอร์ชัน:
* [Docs/LOGGER_AND_AUDIT_GUIDE.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/LOGGER_AND_AUDIT_GUIDE.md)
* [Docs/Changelogs/v1.0.0.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/Changelogs/v1.0.0.md)
* [Docs/USER_GUIDE.md](file:///c:/Users/admin/Documents/HotelPOS/Docs/USER_GUIDE.md)
