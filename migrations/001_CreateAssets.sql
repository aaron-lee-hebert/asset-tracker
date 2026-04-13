CREATE TABLE assets
(
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(100) NOT NULL,
    category    VARCHAR(50)  NOT NULL,
    description VARCHAR(255) NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE
);
