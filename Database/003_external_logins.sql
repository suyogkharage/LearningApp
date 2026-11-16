-- Run against the LearningApp database before deploying the updated API.
-- Safe to run for both existing databases and fresh installations.
IF OBJECT_ID(N'learning.ExternalLogins', N'U') IS NULL
BEGIN
    CREATE TABLE learning.ExternalLogins
    (
        ExternalLoginId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExternalLogins PRIMARY KEY,
        Issuer NVARCHAR(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        Subject NVARCHAR(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_ExternalLogins_Users FOREIGN KEY (UserId)
            REFERENCES learning.Users(UserId) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_ExternalLogins_Issuer_Subject ON learning.ExternalLogins(Issuer, Subject);
    CREATE INDEX IX_ExternalLogins_UserId ON learning.ExternalLogins(UserId);
END;
GO
