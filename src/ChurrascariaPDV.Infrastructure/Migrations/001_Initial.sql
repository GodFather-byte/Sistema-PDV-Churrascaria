CREATE TABLE products (
    id INTEGER PRIMARY KEY,
    sku TEXT NOT NULL COLLATE NOCASE UNIQUE,
    name TEXT NOT NULL,
    category TEXT NOT NULL,
    price_cents INTEGER NOT NULL CHECK(price_cents > 0),
    cost_cents INTEGER CHECK(cost_cents >= 0),
    active INTEGER NOT NULL CHECK(active IN (0, 1)),
    stock INTEGER NOT NULL
);
CREATE TABLE cash_sessions (
    id INTEGER PRIMARY KEY,
    operator TEXT NOT NULL CHECK(length(trim(operator)) > 0),
    opened_at TEXT NOT NULL,
    opening_cents INTEGER NOT NULL CHECK(opening_cents >= 0),
    closed_at TEXT,
    counted_cents INTEGER CHECK(counted_cents >= 0)
);
CREATE UNIQUE INDEX one_open_cash_session ON cash_sessions((1)) WHERE closed_at IS NULL;
CREATE TABLE sales (
    id INTEGER PRIMARY KEY,
    cash_session_id INTEGER NOT NULL REFERENCES cash_sessions(id),
    operator TEXT NOT NULL CHECK(length(trim(operator)) > 0),
    created_at TEXT NOT NULL,
    status INTEGER NOT NULL CHECK(status IN (0, 1, 2)),
    discount_cents INTEGER NOT NULL CHECK(discount_cents >= 0),
    change_cents INTEGER NOT NULL CHECK(change_cents >= 0)
);
CREATE TABLE sale_items (
    sale_id INTEGER NOT NULL REFERENCES sales(id),
    product_id INTEGER NOT NULL REFERENCES products(id),
    sku TEXT NOT NULL,
    name TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    unit_price_cents INTEGER NOT NULL CHECK(unit_price_cents > 0),
    PRIMARY KEY(sale_id, product_id)
);
CREATE TABLE payments (
    sale_id INTEGER NOT NULL REFERENCES sales(id),
    sequence INTEGER NOT NULL,
    method INTEGER NOT NULL CHECK(method IN (0, 1, 2, 3)),
    amount_cents INTEGER NOT NULL CHECK(amount_cents > 0),
    PRIMARY KEY(sale_id, sequence)
);
CREATE TABLE stock_movements (
    id INTEGER PRIMARY KEY,
    product_id INTEGER NOT NULL REFERENCES products(id),
    product_name TEXT NOT NULL,
    type INTEGER NOT NULL CHECK(type IN (0, 1, 2)),
    quantity INTEGER NOT NULL CHECK((type = 2 AND quantity != 0) OR (type IN (0, 1) AND quantity > 0)),
    reason TEXT NOT NULL CHECK(length(trim(reason)) > 0),
    operator TEXT NOT NULL CHECK(length(trim(operator)) > 0),
    created_at TEXT NOT NULL
);
CREATE TABLE cash_movements (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES cash_sessions(id),
    type INTEGER NOT NULL CHECK(type IN (0, 1)),
    amount_cents INTEGER NOT NULL CHECK(amount_cents > 0),
    reason TEXT NOT NULL CHECK(length(trim(reason)) > 0),
    operator TEXT NOT NULL CHECK(length(trim(operator)) > 0),
    created_at TEXT NOT NULL
);
CREATE TABLE audit (
    id INTEGER PRIMARY KEY,
    entity TEXT NOT NULL,
    entity_id INTEGER NOT NULL,
    action TEXT NOT NULL,
    operator TEXT NOT NULL CHECK(length(trim(operator)) > 0),
    created_at TEXT NOT NULL,
    details TEXT NOT NULL
);
CREATE INDEX sales_period ON sales(created_at, status);
CREATE INDEX stock_product ON stock_movements(product_id, id);
PRAGMA user_version = 1;
