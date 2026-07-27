# คู่มือการทดสอบระบบ USB Hardware Dongle & App Serial Watermark (Testing Manual & E2E Guide)

เอกสารนี้ใช้สำหรับทดสอบ (Manual Test) และอ้างอิงชุดทดสอบอัตโนมัติ (E2E xUnit Test) สำหรับระบบลิขสิทธิ์กุญแจฮาร์ดแวร์ USB (USB Hardware Dongle) และรหัสประจำตัวชุดโปรแกรม (App Serial Watermark) ในโปรเจค **HotelPOS TH v1.0.0**

---

## 1. ภาพรวมสถาปัตยกรรมความปลอดภัย (Security Architecture)

```
+-----------------------------------------------------------------------------------+
|                                  HotelPOS TH v1.0.0                               |
+-----------------------------------------------------------------------------------+
                                          |
                        [เปิดโปรแกรม / สุ่มตรวจระหว่างทำงาน]
                                          |
                                          v
                         +---------------------------------+
                         |  สแกนหา USB Flash Drive ที่เสียบอยู่ |
                         +---------------------------------+
                                     /         \
                       พบ USB Dongle           ไม่พบ USB Dongle
                                  /               \
                                 v                 v
            +--------------------------+    +--------------------------+
            | ตรวจสอบ:                  |    | สลับเข้าโหมด Trial 30 วัน  |
            | 1. RSA Digital Signature |    | (Multi-source Anti-Reset |
            | 2. Physical USB Serial   |    |  ยึดวันที่เริ่มเก่าที่สุดเสมอ) |
            | 3. App Serial Watermark  |    +--------------------------+
            | 4. ExpireDate / Clock    |
            | 5. Revocation Blacklist  |
            +--------------------------+
                      /          \
                ถูกต้อง          ไม่ถูกต้อง/คัดลอกมา
                  /                  \
                 v                    v
         [ Full Active ]       [ Invalid / Revoked ]
```

---

## 2. ขั้นตอนการทดสอบด้วยตัวเอง (Manual Testing Guide)

### 2.1 การทดสอบโหมดไม่มี Dongle (Trial 30 วัน Anti-Reset)
1. ดึง USB Flash Drive ออกจากคอมพิวเตอร์ให้หมด
2. เปิดโปรแกรม `HotelPOS.UI.exe` หรือรันผ่าน Admin Tool / Code
3. **ผลลัพธ์ที่คาดหวัง:**
   - โปรแกรมเปิดใช้งานในสถานะ **Trial (ทดลองใช้งาน 30 วัน)**
   - หากทดลองลบฐานข้อมูล SQLite หรือไฟล์ใน AppData ออก แล้วเปิดโปรแกรมใหม่ วันนับถอยหลังต้องนับต่อจากเดิม ไม่รีเซ็ตเป็น 30 วันใหม่

---

### 2.2 การทดสอบสร้างและเปิดใช้งาน USB Dongle ผ่าน License Admin Tool
1. เปิดโปรแกรม `HotelPOS.LicenseAdminTool.exe`
2. เสียบ USB Flash Drive ของนักพัฒนาเข้ากับคอมพิวเตอร์
3. ในโซนล่าง **"🔑 ศูนย์ทดสอบและออกกุญแจ USB Hardware Dongle"**:
   - กดปุ่ม **"🔄 รีเฟรช"** -> รายชื่อ USB Drive จะปรากฏใน ComboBox
   - กรอกชื่อลูกค้า และตั้งค่าประเภทสิทธิ์เป็น `Lifetime` หรือ `Standard`
   - กดปุ่ม **"💾 Gen & เขียน dongle.key ลง USB"** -> โปรแกรมจะอ่าน **Physical Hardware Serial** ของชิป USB แล้วสร้างไฟล์ `dongle.key` เขียนลงใน USB Flash Drive ทันที
4. กดปุ่ม **"⚡ ทดสอบปลดล็อก"**
5. **ผลลัพธ์ที่คาดหวัง:**
   - ขึ้นป๊อปอัปแจ้งเตือน: `✅ ปลดล็อกใช้งานระบบสำเร็จ 100% (Dongle Active)` แสดง Physical USB Serial และสถานะ Active

---

### 2.3 การทดสอบกฎเหล็ก: ป้องกันการคัดลอกไฟล์ `dongle.key` ไปยัง Flash Drive อันอื่น
1. ก็อปปี้ไฟล์ `dongle.key` จาก USB Flash Drive A ไปวางใส่ USB Flash Drive B
2. เสียบ USB Flash Drive B เข้าคอมพิวเตอร์
3. กดปุ่ม **"⚡ ทดสอบปลดล็อก"** หรือเปิดแอป `HotelPOS.UI`
4. **ผลลัพธ์ที่คาดหวัง:**
   - ระบบปฏิเสธการใช้งาน (`LicenseStatus.Invalid`) เนื่องจาก Hardware Serial ระดับชิปของ Flash Drive B ไม่ตรงกับลายเซ็นดิจิทัลใน `dongle.key`

---

### 2.4 การทดสอบกฎเหล็ก: ป้องกันการนำ Dongle ไปใช้ข้ามชุด App Serial Watermark
1. ใน Admin Tool กดปุ่ม **"🏷️ Gen app.watermark"** โดยกำหนด App Serial เป็น `APP-CLIENT-A`
2. สร้าง `dongle.key` สำหรับ `APP-CLIENT-A` ลง USB Flash Drive
3. เปลี่ยนไฟล์ `app.watermark` ของแอปให้เป็น `APP-CLIENT-B`
4. **ผลลัพธ์ที่คาดหวัง:**
   - ระบบปฏิเสธการปลดล็อก (`LicenseStatus.Invalid`) เนื่องจาก App Serial ไม่ตรงกัน

---

## 3. การรันชุดทดสอบอัตโนมัติ (Automated E2E xUnit Test Commands)

เปิด PowerShell หรือ Terminal ในโฟลเดอร์โปรเจค แล้วรันคำสั่ง:

```powershell
dotnet test tests/HotelPOS.Tests/HotelPOS.Tests.csproj -p:RollForward=Major
```

### รายการเคสที่ต้องผ่านทั้งหมด (100% Passed):
- `LicensingTests.UsbDongle_PhysicalSerial_ควรคำนวณHashได้สมบูรณ์`
- `LicensingTests.UsbDongle_นำไฟล์ก๊อปปี้ไปFlashDriveอันอื่น_ควรได้สถานะ_Invalid`
- `LicensingTests.UsbDongle_นำDongleของAppAไปใช้กับAppB_ควรได้สถานะ_Invalid`
- `LicensingTests.UsbDongle_ข้อมูลถูกต้องและAppSerialตรง_ควรได้สถานะ_Active`
- `LicensingTests.TrialManager_เช็คครั้งแรก_ควรได้วันใช้งานเหลือสามสิบวันเต็ม`
- `LicensingTests.TrialManager_หากมีข้อมูลไม่ตรงกัน_ควรปรับซิงค์คืนค่าวันที่เริ่มเก่าที่สุด`
- `LicensingTests.LicenseValidation_ย้อนเวลาเครื่องก่อนหน้า_lastVerifiedAt_ควรได้สถานะ_Invalid`
- `LicensingTests.LicenseValidation_อยู่ในรายการถอนสิทธิ์_Revoked_ควรได้สถานะ_Revoked`
