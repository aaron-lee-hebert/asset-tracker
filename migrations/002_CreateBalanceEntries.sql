CREATE TABLE BalanceEntries
(
    Id INT IDENTITY PRIMARY KEY,
    AssetId INT NOT NULL REFERENCES Assets(Id),
    Balance DECIMAL(18, 2) NOT NULL,
    RecordedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Note NVARCHAR(255) NULL
);

CREATE INDEX IX_BalanceEntries_AssetId_RecordedAt
    ON BalanceEntries (AssetId, RecordedAt DESC);