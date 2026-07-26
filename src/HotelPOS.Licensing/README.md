# HotelPOS.Licensing

โมดูลนี้ยังไม่มีโค้ด — จะเขียนใน **Phase 1: Core License** ตาม Roadmap (PROJECT_PLAN.md ข้อ 8)

เมื่อถึงเวลาพัฒนา ให้สร้างไฟล์อย่างน้อย:
- `HardwareIdGenerator.cs` — สร้าง Hardware ID จาก CPU+Disk+MAC
- `LicenseFile.cs` — โมเดลข้อมูลใน license.dat
- `LicenseValidator.cs` — ตรวจสอบลายเซ็น RSA (เก็บเฉพาะ **Public Key**)
- `LicenseManager.cs` — API หลักที่ HotelPOS.UI เรียกใช้ (IsValid, DaysRemaining, ActivateAsync ฯลฯ)
- `TrialManager.cs` — จัดการ Trial 30 วัน (เขียนหลายจุดกัน reset)

**ห้ามใส่ RSA Private Key ในโปรเจคนี้เด็ดขาด** (อยู่ใน HotelPOS.LicenseAdminTool เท่านั้น — ดู SKILL.md ข้อ 5)
