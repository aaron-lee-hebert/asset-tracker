CREATE TABLE balance_entries
(
    id          SERIAL PRIMARY KEY,
    asset_id    INT            NOT NULL REFERENCES assets(id),
    balance     DECIMAL(18, 2) NOT NULL,
    recorded_at TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    note        VARCHAR(255)   NULL
);

CREATE INDEX ix_balance_entries_asset_id_recorded_at
    ON balance_entries (asset_id, recorded_at DESC);
