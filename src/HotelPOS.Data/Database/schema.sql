-- ============================================================
-- HotelPOS TH - Database Schema (SQLite)
-- Schema Version: 1
-- หมายเหตุ: สคริปต์นี้เขียนแบบ idempotent (IF NOT EXISTS) เพื่อรันซ้ำได้ปลอดภัย
-- ============================================================

PRAGMA foreign_keys = ON;

-- ---------- schema_migrations : ใช้ track เวอร์ชัน schema ----------
CREATE TABLE IF NOT EXISTS schema_migrations (
    version     INTEGER PRIMARY KEY,
    applied_at  TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    description TEXT
);

-- ---------- roles / users ----------
CREATE TABLE IF NOT EXISTS roles (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    name              TEXT NOT NULL UNIQUE,
    permissions_json  TEXT NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS users (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    username       TEXT NOT NULL UNIQUE,
    password_hash  TEXT NOT NULL,
    full_name      TEXT NOT NULL,
    role_id        INTEGER NOT NULL REFERENCES roles(id),
    is_active      INTEGER NOT NULL DEFAULT 1,
    last_login_at  TEXT,
    created_at     TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- ---------- room_types / rooms ----------
CREATE TABLE IF NOT EXISTS room_types (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT NOT NULL,
    daily_rate   NUMERIC NOT NULL DEFAULT 0,
    hourly_rate  NUMERIC NOT NULL DEFAULT 0,
    monthly_rate NUMERIC NOT NULL DEFAULT 0,
    description  TEXT,
    is_active    INTEGER NOT NULL DEFAULT 1,
    electric_billing_mode INTEGER NOT NULL DEFAULT 0, -- 0=Meter, 1=FlatRate
    electric_flat_rate NUMERIC NOT NULL DEFAULT 0,
    water_billing_mode INTEGER NOT NULL DEFAULT 0,    -- 0=Meter, 1=FlatRate
    water_flat_rate NUMERIC NOT NULL DEFAULT 0,
    color_hex    TEXT DEFAULT '#3B82F6',
    created_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS rooms (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    room_number  TEXT NOT NULL UNIQUE,
    room_type_id INTEGER NOT NULL REFERENCES room_types(id),
    floor        TEXT,
    status       INTEGER NOT NULL DEFAULT 0,   -- ดู enum RoomStatus
    notes        TEXT,
    is_active    INTEGER NOT NULL DEFAULT 1,
    created_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
CREATE INDEX IF NOT EXISTS idx_rooms_status ON rooms(status);

-- ---------- customers ----------
CREATE TABLE IF NOT EXISTS customers (
    id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    full_name            TEXT NOT NULL,
    phone                TEXT,
    email                TEXT,
    id_card_or_passport  TEXT,
    address              TEXT,
    notes                TEXT,
    created_at           TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    is_deleted           INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_customers_phone ON customers(phone);

-- ---------- bookings / folios ----------
CREATE TABLE IF NOT EXISTS bookings (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    booking_code       TEXT NOT NULL UNIQUE,
    room_id            INTEGER NOT NULL REFERENCES rooms(id),
    customer_id        INTEGER NOT NULL REFERENCES customers(id),
    rate_plan          INTEGER NOT NULL DEFAULT 0,   -- RatePlanType
    check_in_planned   TEXT NOT NULL,
    check_out_planned  TEXT,
    check_in_actual    TEXT,
    check_out_actual   TEXT,
    status             INTEGER NOT NULL DEFAULT 0,   -- BookingStatus
    agreed_rate        NUMERIC NOT NULL DEFAULT 0,
    notes              TEXT,
    created_by         INTEGER REFERENCES users(id),
    created_at         TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at         TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    is_deleted         INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_bookings_room ON bookings(room_id);
CREATE INDEX IF NOT EXISTS idx_bookings_status ON bookings(status);
CREATE INDEX IF NOT EXISTS idx_bookings_checkin ON bookings(check_in_planned);

CREATE TABLE IF NOT EXISTS folios (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    booking_id     INTEGER NOT NULL REFERENCES bookings(id),
    is_closed      INTEGER NOT NULL DEFAULT 0,
    room_charges   NUMERIC NOT NULL DEFAULT 0,
    extra_charges  NUMERIC NOT NULL DEFAULT 0,
    discount_amount NUMERIC NOT NULL DEFAULT 0,
    total_amount   NUMERIC NOT NULL DEFAULT 0,
    created_at     TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    closed_at      TEXT
);

-- ---------- products / sales ----------
CREATE TABLE IF NOT EXISTS product_categories (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    name      TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS products (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id  INTEGER NOT NULL REFERENCES product_categories(id),
    name         TEXT NOT NULL,
    sku          TEXT,
    price        NUMERIC NOT NULL DEFAULT 0,
    cost         NUMERIC NOT NULL DEFAULT 0,
    stock_qty    INTEGER NOT NULL DEFAULT 0,
    track_stock  INTEGER NOT NULL DEFAULT 0,
    is_active    INTEGER NOT NULL DEFAULT 1,
    created_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS sales (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_code        TEXT NOT NULL UNIQUE,
    folio_id         INTEGER REFERENCES folios(id),
    customer_id      INTEGER REFERENCES customers(id),
    sub_total        NUMERIC NOT NULL DEFAULT 0,
    discount_amount  NUMERIC NOT NULL DEFAULT 0,
    tax_amount       NUMERIC NOT NULL DEFAULT 0,
    total_amount     NUMERIC NOT NULL DEFAULT 0,
    created_by       INTEGER REFERENCES users(id),
    created_at       TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    is_deleted       INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_sales_created_at ON sales(created_at);

CREATE TABLE IF NOT EXISTS sale_items (
    id                     INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id                INTEGER NOT NULL REFERENCES sales(id),
    product_id             INTEGER NOT NULL REFERENCES products(id),
    product_name_snapshot  TEXT NOT NULL,
    unit_price             NUMERIC NOT NULL,
    quantity               INTEGER NOT NULL,
    line_total             NUMERIC NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_sale_items_sale ON sale_items(sale_id);

CREATE TABLE IF NOT EXISTS payments (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id       INTEGER NOT NULL REFERENCES sales(id),
    method        INTEGER NOT NULL,  -- PaymentMethod
    amount        NUMERIC NOT NULL,
    reference_no  TEXT,
    paid_at       TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    received_by   INTEGER REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS invoice_documents (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id             INTEGER NOT NULL REFERENCES sales(id),
    doc_type            INTEGER NOT NULL,  -- DocumentType
    document_number     TEXT NOT NULL UNIQUE,
    printed_paper_size  INTEGER NOT NULL,  -- PaperSize
    printed_at          TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    printed_by          INTEGER REFERENCES users(id),
    print_count         INTEGER NOT NULL DEFAULT 1
);

-- ---------- settings / audit / license / backup ----------
CREATE TABLE IF NOT EXISTS settings (
    key          TEXT PRIMARY KEY,
    value        TEXT,
    description  TEXT,
    updated_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id      INTEGER REFERENCES users(id),
    action       TEXT NOT NULL,
    entity_name  TEXT,
    entity_id    TEXT,
    detail_json  TEXT,
    created_at   TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at);

-- หมายเหตุ: ไฟล์ license จริงเก็บแบบเข้ารหัสแยกต่างหาก (license.dat)
-- ตารางนี้เก็บสำเนา/สถานะ เพื่อแสดงผลในโปรแกรมเท่านั้น ไม่ใช่แหล่งความจริง (source of truth)
CREATE TABLE IF NOT EXISTS license_info (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_name      TEXT NOT NULL,
    hardware_id        TEXT NOT NULL,
    license_type       INTEGER NOT NULL,  -- LicenseType
    issue_date         TEXT NOT NULL,
    expire_date        TEXT,               -- NULL = ถาวร
    max_rooms          INTEGER,
    features_json      TEXT NOT NULL DEFAULT '[]',
    status             INTEGER NOT NULL DEFAULT 4, -- LicenseStatus.NotActivated
    last_verified_at   TEXT
);

CREATE TABLE IF NOT EXISTS backup_history (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    file_path      TEXT NOT NULL,
    checksum       TEXT NOT NULL,
    is_auto_backup INTEGER NOT NULL DEFAULT 0,
    performed_by   TEXT,
    type           TEXT NOT NULL DEFAULT 'BACKUP', -- BACKUP | RESTORE | RESET
    created_at     TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- ---------- meter_readings : บันทึกเลขมิเตอร์ค่าน้ำ/ค่าไฟ รายห้อง ----------
CREATE TABLE IF NOT EXISTS meter_readings (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    room_id        INTEGER NOT NULL REFERENCES rooms(id),
    utility_type   INTEGER NOT NULL,             -- 0=ELECTRIC, 1=WATER (UtilityType enum)
    billing_month  TEXT NOT NULL,                 -- 'YYYY-MM' เช่น '2026-07'
    reading_prev   NUMERIC NOT NULL DEFAULT 0,   -- เลขมิเตอร์เดือนก่อน
    reading_curr   NUMERIC NOT NULL DEFAULT 0,   -- เลขมิเตอร์เดือนนี้
    units_used     NUMERIC NOT NULL DEFAULT 0,   -- หน่วยที่ใช้ (curr - prev)
    rate_per_unit  NUMERIC NOT NULL DEFAULT 0,   -- อัตราค่าหน่วย ณ ตอนบันทึก (snapshot)
    total_amount   NUMERIC NOT NULL DEFAULT 0,   -- ยอดรวม = units_used × rate_per_unit
    recorded_by    INTEGER REFERENCES users(id),
    recorded_at    TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    notes          TEXT,
    UNIQUE(room_id, utility_type, billing_month)  -- 1 ห้อง 1 ประเภท 1 เดือน ห้ามซ้ำ
);
CREATE INDEX IF NOT EXISTS idx_meter_readings_room ON meter_readings(room_id);
CREATE INDEX IF NOT EXISTS idx_meter_readings_month ON meter_readings(billing_month);

-- ---------- utility_bills : ใบแจ้งหนี้ค่าสาธารณูปโภครายเดือน ----------
CREATE TABLE IF NOT EXISTS utility_bills (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    bill_code           TEXT NOT NULL UNIQUE,        -- เลขที่บิล เช่น UB-202607-0001
    room_id             INTEGER NOT NULL REFERENCES rooms(id),
    billing_month       TEXT NOT NULL,               -- 'YYYY-MM'
    room_charge         NUMERIC NOT NULL DEFAULT 0,  -- ค่าเช่าห้อง
    
    -- มิเตอร์ไฟ snapshot
    electric_prev       NUMERIC NOT NULL DEFAULT 0,  -- เลขไฟก่อน
    electric_curr       NUMERIC NOT NULL DEFAULT 0,  -- เลขไฟหลัง
    electric_units      NUMERIC NOT NULL DEFAULT 0,  -- หน่วยไฟที่ใช้
    electric_rate       NUMERIC NOT NULL DEFAULT 0,  -- อัตราค่าไฟ/หน่วย
    electric_amount     NUMERIC NOT NULL DEFAULT 0,  -- ค่าไฟรวม
    electric_billing_mode TEXT NOT NULL DEFAULT 'METER', -- METER / FLAT
    
    -- มิเตอร์น้ำ snapshot
    water_prev          NUMERIC NOT NULL DEFAULT 0,  -- เลขอ้นก่อน
    water_curr          NUMERIC NOT NULL DEFAULT 0,  -- เลขอ้นหลัง
    water_units         NUMERIC NOT NULL DEFAULT 0,  -- หน่วยน้ำที่ใช้
    water_rate          NUMERIC NOT NULL DEFAULT 0,  -- อัตราค่าน้ำ/หน่วย
    water_amount        NUMERIC NOT NULL DEFAULT 0,  -- ค่าน้ำรวม
    water_billing_mode  TEXT NOT NULL DEFAULT 'METER', -- METER / FLAT
    water_person_count  INTEGER NOT NULL DEFAULT 1,  -- จำนวนคนในห้อง (ใช้เมื่อ FLAT)
    
    common_area_fee     NUMERIC NOT NULL DEFAULT 0,  -- ค่าส่วนกลาง/ค่าบริการ
    garbage_fee         NUMERIC NOT NULL DEFAULT 0,  -- ค่าขยะ
    extra_charges       NUMERIC NOT NULL DEFAULT 0,  -- ค่าอื่นๆ เพิ่มเติม
    discount_amount     NUMERIC NOT NULL DEFAULT 0,
    total_amount        NUMERIC NOT NULL DEFAULT 0,  -- ยอดรวมทั้งหมด
    is_paid             INTEGER NOT NULL DEFAULT 0,
    paid_at             TEXT,
    payment_method      INTEGER,                     -- PaymentMethod enum
    created_by          INTEGER REFERENCES users(id),
    created_at          TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    notes               TEXT,
    UNIQUE(room_id, billing_month)                   -- 1 ห้อง มี 1 บิล ต่อ 1 เดือน
);
CREATE INDEX IF NOT EXISTS idx_utility_bills_room ON utility_bills(room_id);
CREATE INDEX IF NOT EXISTS idx_utility_bills_month ON utility_bills(billing_month);
CREATE INDEX IF NOT EXISTS idx_utility_bills_room_month ON utility_bills(room_id, billing_month);

-- ---------- ค่าเริ่มต้น (seed) ----------
INSERT OR IGNORE INTO roles (id, name, permissions_json) VALUES
    (1, 'ผู้ดูแลระบบ', '{"all":true}'),
    (2, 'พนักงานหน้าเคาน์เตอร์', '{"booking":true,"pos":true}');

INSERT OR IGNORE INTO users (id, username, password_hash, full_name, role_id) VALUES
    (1, 'admin', 'admin', 'ผู้ดูแลระบบ', 1);

INSERT OR IGNORE INTO settings (key, value, description) VALUES
    ('shop_name', 'ชื่อร้าน/โรงแรมของคุณ', 'ชื่อร้านที่แสดงบนใบเสร็จ'),
    ('shop_address', '', 'ที่อยู่ร้าน'),
    ('shop_phone', '', 'เบอร์โทรร้าน'),
    ('shop_tax_id', '', 'เลขประจำตัวผู้เสียภาษี'),
    ('shop_logo_path', '', 'พาธไฟล์โลโก้ร้าน'),
    ('receipt_doc_prefix', 'RC', 'คำนำหน้าเลขที่ใบเสร็จ'),
    ('receipt_doc_running_number', '0', 'เลขที่เอกสารล่าสุดที่ออกไปแล้ว'),
    ('default_printer_name', '', 'ชื่อเครื่องพิมพ์เริ่มต้น'),
    ('default_paper_size', '1', 'ขนาดกระดาษเริ่มต้น (ดู enum PaperSize)'),
    ('backup_auto_enabled', '1', 'เปิด/ปิด backup อัตโนมัติ'),
    ('backup_retention_days', '90', 'จำนวนวันเก็บ backup อัตโนมัติ'),
    ('electric_rate_per_unit', '8.00', 'ค่าไฟฟ้าต่อหน่วย (บาท)'),
    ('water_billing_mode', 'METER', 'โหมดคิดค่าน้ำ: METER=ตามมิเตอร์, FLAT=เหมาจ่ายรายคน'),
    ('water_rate_per_unit', '18.00', 'ค่าน้ำประปาต่อหน่วย (บาท) - ใช้เมื่อ mode=METER'),
    ('water_flat_rate_per_person', '100.00', 'ค่าน้ำเหมาจ่ายต่อคน (บาท) - ใช้เมื่อ mode=FLAT'),
    ('common_area_fee', '0', 'ค่าส่วนกลาง/ค่าบริการรายเดือน (บาท)'),
    ('garbage_fee', '0', 'ค่าขยะรายเดือน (บาท)');

INSERT INTO schema_migrations (version, description)
    SELECT 1, 'Initial schema'
    WHERE NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = 1);

