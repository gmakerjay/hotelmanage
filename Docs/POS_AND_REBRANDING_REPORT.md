# รายงานความคืบหน้าการ Rebrand และพัฒนาระบบ POS (PSoft Rest & Rent Manager)

> **เวอร์ชันระบบ**: `1.0.0`  
> **วันอัปเดตล่าสุด**: 27 กรกฎาคม 2026  
> **เอกสารรายงานหลัก**: จัดทำขึ้นตามข้อกำหนดการพัฒนาและรายงานความคืบหน้าในระดับโปรดักชัน

---

## 1. การดำเนินการ Rebrand และแยกส่วนการทำงาน (White-Labeling & Isolation)

เพื่อเปลี่ยนชื่อแอปพลิเคชันจากชื่อชั่วคราว "HotelPOS" ไปเป็นชื่อแบรนด์อย่างเป็นทางการ **"PSoft Rest & Rent Manager"** ทางผู้พัฒนาได้ดำเนินการปรับปรุงโครงสร้างของโค้ดดังนี้:

### 1.1 ไฟล์รันโปรแกรมและเอาต์พุตไบนารี (Assembly Names)
- แก้ไข [HotelPOS.UI.csproj](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.UI/HotelPOS.UI.csproj) กำหนดชื่อ Assembly เป็น `<AssemblyName>PSoftRestRentManager</AssemblyName>` ส่งผลให้โปรแกรมหลักคอมไพล์ออกมาในชื่อ `PSoftRestRentManager.exe`
- แก้ไข [HotelPOS.LicenseAdminTool.csproj](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.LicenseAdminTool/HotelPOS.LicenseAdminTool.csproj) กำหนดชื่อ Assembly เป็น `<AssemblyName>PSoftRestRentGenerator</AssemblyName>` ส่งผลให้เครื่องมือผู้เขียนคีย์คอมไพล์ออกเป็นชื่อ `PSoftRestRentGenerator.exe`

### 1.2 การแยกตำแหน่งฐานข้อมูล ล็อกระบบ และ Registry (Data Isolation)
- **โฟลเดอร์เก็บข้อมูลจริง (AppData)**: ปรับตำแหน่งจัดเก็บไฟล์ไปที่ `%AppData%\PSoftRestRentManager\` ป้องกันการเขียนทับหรือมีผลกระทบกับข้อมูลเดิมของโปรแกรมอื่น
- **ชื่อฐานข้อมูล SQLite**: ย้ายตำแหน่งและเปลี่ยนชื่อเป็น `%AppData%\PSoftRestRentManager\restrent.db` (เดิมคือ `hotelpos.db`)
- **โฟลเดอร์เก็บ Log**: เปลี่ยนไปจัดเก็บที่ `%AppData%\PSoftRestRentManager\logs\`
- **คีย์ Registry**: เปลี่ยนโฟลเดอร์หลักของคีย์ทดลองใช้งานจาก `Software\HotelPOS` ไปเป็น `Software\PSoftRestRentManager`

### 1.3 การปรับแต่งชื่อแบรนด์และป้ายข้อความหลัก (UI Brand Transition)
- **แถบชื่อโปรแกรม**: ปรับเป็น `"PSoft Rest & Rent Manager - โปรแกรมจัดการห้องพักและห้องเช่า"`
- **ส่วนหัวไซด์บาร์นำทาง (Sidebar Brand Panel)**: ปรับแถบแบรนด์หลักเป็น `"PSoft R&R"` และป้ายคำอธิบายย่อยเป็น `"Rest & Rent Manager"`
- **ค่าเริ่มต้นระบบ**: กำหนดชื่อร้านเริ่มต้นเป็น `"PSoft Rest & Rent Manager"` พร้อมข้อความต้อนรับและขอบคุณเริ่มต้นที่สอดคล้องกับแบรนด์ใหม่
- **เครื่องมือ KeyGen**: เปลี่ยนหัวเรื่องการทำงานและข้อความแจ้งเตือนต่างๆ ให้สอดคล้องกัน และใช้ป้าย Volume Label ของ USB Dongle ลิขสิทธิ์แท้เป็น `REST_RENT_KEY`

---

## 2. การพัฒนาระบบ POS บริการเสริม & มินิบาร์ (POS Shop Billing Module)

พัฒนาระบบ POS และการจัดการสินค้า/สต็อก ตามเป้าหมาย Roadmap Phase 3 บนฐานข้อมูล SQLite ที่มีอยู่:

### 2.1 โครงสร้างระดับที่จัดเก็บข้อมูล (Data Repository Layer)
- พัฒนาคลาส [ProductRepository.cs](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.Data/Repositories/ProductRepository.cs):
  - ดึงข้อมูลประเภทสินค้า (`product_categories`)
  - ค้นหาและคัดกรองข้อมูลสินค้าแยกตามประเภทหรือคำค้นหา (`products`)
  - บันทึกการเพิ่ม/แก้ไขสินค้า และปรับสต็อกสินค้าคงเหลือเมื่อมีการจำหน่ายสินค้าออก
- พัฒนาคลาส [SaleRepository.cs](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.Data/Repositories/SaleRepository.cs):
  - สร้างธุรกรรมขายสินค้าในแบบ Transaction: บันทึกข้อมูลใบเสร็จลงในตาราง `sales`, บันทึกรายการย่อยลงตาราง `sale_items`, บันทึกช่องทางการชำระเงินลงตาราง `payments`
  - ทำการตัดจำนวนสต็อกสินค้าในกรณีที่เปิดระบบควบคุมสต็อกสินค้า
  - **การชาร์จเข้า Folio ห้องพัก**: รองรับการนำยอดสุทธิของการขายไปอัปเดตลงในบัญชีบิลค้างจ่ายของห้องพัก (`extra_charges` และ `total_amount` ในตาราง `folios`)

### 2.2 โครงสร้างระดับตรรกะระบบ (Business Logic Layer)
- พัฒนาคลาส [POSService.cs](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.Core/Services/POSService.cs):
  - ตรวจสอบความถูกต้องของราคาสินค้า ณ เวลาขาย และตรวจสอบจำนวนสต็อกก่อนปิดยอด
  - คำนวณยอดรวม (Subtotal) ส่วนลด (Discount) และยอดรวมสุทธิอย่างละเอียด
  - ออกรหัสบิลใบเสร็จใหม่อัตโนมัติในฟอร์แมต `SL-yyyyMMddHHmmss-XXX`

### 2.3 ส่วนต่อประสานงานผู้ใช้ (User Interface Layer)
- พัฒนาหน้าจอหลัก [POSControl.cs](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.UI/POSControl.cs):
  - **ฝั่งซ้าย (รายการสินค้า)**: แถบปุ่มกรองประเภทสินค้า (Category Tabs) กล่องค้นหา และ Grid แสดงสินค้าเป็นแบบ Visual Cards แสดงราคา จำนวนสต็อก และปุ่มด่วน "+ เพิ่ม"
  - **ฝั่งขวา (ตะกร้าสินค้า & ปิดยอด)**: แสดงรายการสินค้าที่เลือก สามารถดับเบิ้ลคลิกเพื่อแก้ไขจำนวนหรือลบรายการออกจากตะกร้าได้
  - **ระบบชาร์จห้องพัก**: ช่องเลือกชาร์จเข้าห้องพัก (Folio) โดยจะดึงเฉพาะห้องพักที่กำลังมีผู้เช็คอินอยู่ (`CheckedIn` status) มาแสดงผลให้เลือกชาร์จได้อย่างถูกต้องแม่นยำ
  - **หน้าจอย่อยจัดการสินค้าและสต็อก (Inline Inventory Manager)**: เพิ่มปุ่มให้แอดมินสามารถเข้าไป เพิ่ม/แก้ไข สินค้า ประเภทสินค้า ราคาทุน ราคาขาย และใส่จำนวนสต็อกได้ทันทีภายในตัวแอป
- พัฒนาหน้าจอรับเงิน [POSPaymentForm.cs](file:///c:/Users/admin/Documents/Photel/src/HotelPOS.UI/POSPaymentForm.cs):
  - รับเงินระบุช่องทาง (เงินสด, โอนเงิน, บัตรเครดิต, พร้อมเพย์) พร้อมคำนวณเงินทอนอัตโนมัติ และถามความต้องการพิมพ์ใบเสร็จความร้อน (Slip Receipt) บนเครื่องพิมพ์ 58mm/80mm

---

## 3. สรุปผลการทดสอบและการจัดทำ Build เพื่อจัดส่งมอบ (Compilation & Testing & Publish)

### 3.1 ผลการทดสอบ Unit Tests
- ทำการรันคำสั่งรวบรวมชุดทดสอบทั้งหมด: `dotnet test`
- **ผลลัพธ์**: ผ่านการตรวจสอบความถูกต้อง 100% ทั้งหมด 36/36 เคสย่อย ไม่มีเคสใดล้มเหลวหรือถูกข้าม

### 3.2 ผลการแพคเกจไบนารีระดับโปรดักชัน
- ทำการรันคอมไพล์สไตล์ Self-Contained สำหรับ Windows x86 (สอดคล้องกับคุณสมบัติของเครื่องคอมพิวเตอร์รุ่นเก่าทั่วไป):
  - **โฟลเดอร์สำหรับผู้ใช้งานหลัก**: `C:\Users\admin\Desktop\STDeploy\HotelPOS_Full\`
    - เอาต์พุตโปรแกรมหลัก: `PSoftRestRentManager.exe`
  - **โฟลเดอร์สำหรับผู้ขาย (เครื่องมือ Gen คีย์)**: `C:\Users\admin\Desktop\STDeploy\HotelPOS_Tools\`
    - เอาต์พุตเครื่องมือ: `PSoftRestRentGenerator.exe`
- **ความปลอดภัย**: ทำการลบไฟล์สัญลักษณ์ดีบัก (`*.pdb`) ออกจากทุกโฟลเดอร์เอาต์พุตเรียบร้อยแล้ว
