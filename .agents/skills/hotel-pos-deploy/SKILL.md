---
name: hotel-pos-deploy
description: >
  ใช้ Skill นี้ทุกครั้งที่ต้อง Build, Publish, Deploy โปรเจค HotelPOS
  ไปยังโฟลเดอร์ STDeploy บน Desktop สำหรับเตรียมส่งมอบลูกค้า
  รวมถึงการ Build ทั้ง Main App (HotelPOS.UI) และ KeyGen Tool (LicenseAdminTool)
  ครอบคลุมคำสั่ง dotnet publish, การทำ Self-Contained,
  และการลบ debug symbols (.pdb) ออกจาก production build
---

# Hotel POS Deploy — Agent Skill

Skill สำหรับ Build + Deploy โปรเจค HotelPOS v1.0.0 ไปยังโฟลเดอร์ส่งมอบ

## 1. Deploy Target Structure

STDeploy เป็นโฟลเดอร์รวมโปรแกรมทั้งหมดของนักพัฒนา (StorePOS, HotelPOS ฯลฯ)
**ห้ามยุ่งกับโฟลเดอร์ของโปรแกรมอื่น (เช่น StorePOS_Full, Tools)**

```
C:\Users\admin\Desktop\STDeploy\
├── StorePOS_Full/           ← StorePOS (โปรแกรมอื่น — ห้ามแตะ!)
├── Tools/                   ← StorePOS tools (โปรแกรมอื่น — ห้ามแตะ!)
│
├── HotelPOS_Full/           ← HotelPOS Main App (สำหรับลูกค้า)
│   ├── HotelPOS.UI.exe      ← ตัวโปรแกรมหลัก
│   ├── HotelPOS.*.dll       ← DLL ของโปรเจค
│   ├── *.dll                ← .NET Runtime + Dependencies (~260 ไฟล์, ~141 MB)
│   └── ...
│
└── HotelPOS_Tools/          ← HotelPOS เครื่องมือนักพัฒนา (ห้ามแจกลูกค้า!)
    ├── HotelPOS.LicenseAdminTool.exe  ← KeyGen + USB Dongle Writer
    ├── *.dll                ← .NET Runtime + Dependencies (~254 ไฟล์, ~136 MB)
    └── ...
```

## 2. Build Commands (ทำตามลำดับ)

### Step 1: Clean Solution
```powershell
cd c:\Users\admin\Documents\Photel
dotnet clean -c Release
```

### Step 2: Publish Main App (HotelPOS.UI) — แยกไฟล์ไบนารี่
```powershell
dotnet publish src/HotelPOS.UI/HotelPOS.UI.csproj `
  -c Release `
  -r win-x86 `
  --self-contained true `
  -o "C:\Users\admin\Desktop\STDeploy\HotelPOS_Full"
```

### Step 3: Publish KeyGen Tool (LicenseAdminTool) — แยกไฟล์ไบนารี่
```powershell
dotnet publish src/HotelPOS.LicenseAdminTool/HotelPOS.LicenseAdminTool.csproj `
  -c Release `
  -r win-x86 `
  --self-contained true `
  -o "C:\Users\admin\Desktop\STDeploy\HotelPOS_Tools"
```

### Step 4: Cleanup Debug Symbols
```powershell
Remove-Item "C:\Users\admin\Desktop\STDeploy\HotelPOS_Full\*.pdb" -Force
Remove-Item "C:\Users\admin\Desktop\STDeploy\HotelPOS_Tools\*.pdb" -Force
```

## 3. Build Configuration

| Parameter | Value | เหตุผล |
|---|---|---|
| Configuration | `Release` | Optimized, no debug info |
| RuntimeIdentifier | `win-x86` | เข้ากันได้สูงสุดกับเครื่องเก่า (32-bit + 64-bit) |
| SelfContained | `true` | ไม่ต้องติดตั้ง .NET Runtime บนเครื่องลูกค้า |
| PublishSingleFile | **ไม่ใช้** | แยกไฟล์ไบนารี่ เพื่อให้ผู้ใช้แพ็คตัวติดตั้งได้เอง |

## 4. Post-Deploy Checklist

- [ ] ลบไฟล์ .pdb ทั้งหมดจาก output (ไม่ส่ง debug symbols ให้ลูกค้า)
- [ ] ตรวจสอบว่า `HotelPOS.UI.exe` รันได้โดยไม่ต้องติดตั้ง .NET Runtime
- [ ] ตรวจสอบว่า `HotelPOS.LicenseAdminTool.exe` รันได้บนเครื่องนักพัฒนา
- [ ] **ห้ามส่ง `HotelPOS_Tools/` ไปให้ลูกค้าเด็ดขาด** (มี Private Key)
- [ ] ไม่ต้อง Zip/RAR — ผู้ใช้จัดการแพ็คไฟล์/ตัวติดตั้งเอง

## 5. Versioning

- **เวอร์ชัน: 1.0.0** (ตามกฎจาก AGENTS.md — ห้ามเปลี่ยนจนกว่าผู้ใช้จะสั่ง)

## 6. ข้อควรระวัง

> [!CAUTION]
> **ห้ามส่งโฟลเดอร์ `HotelPOS_Tools/` ไปให้ลูกค้าเด็ดขาด!**
> มี RSA Private Key ที่ใช้เซ็น License

> [!IMPORTANT]
> **ห้ามยุ่งกับโฟลเดอร์ `StorePOS_Full/` และ `Tools/`**
> เป็นโปรแกรมคนละตัว อย่าลบ/เขียนทับ/แก้ไขไฟล์ใดๆ ในนั้น
