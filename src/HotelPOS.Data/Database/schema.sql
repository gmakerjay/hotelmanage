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
    ('backup_retention_days', '90', 'จำนวนวันเก็บ backup อัตโนมัติ');

INSERT INTO schema_migrations (version, description)
    SELECT 1, 'Initial schema'
    WHERE NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = 1);
