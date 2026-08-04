IF DB_ID(N'LearningAppDb') IS NULL
BEGIN
    CREATE DATABASE LearningAppDb;
END
GO

USE LearningAppDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'learning')
BEGIN
    EXEC(N'CREATE SCHEMA learning');
END
GO

CREATE TABLE learning.Users
(
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    Role NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Role DEFAULT N'Learner',
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Users_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(7) NULL
);
GO

CREATE UNIQUE INDEX UX_Users_Email ON learning.Users(Email);
GO

CREATE TABLE learning.Categories
(
    CategoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,
    CreatedUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Categories_CreatedUtc DEFAULT SYSUTCDATETIME()
);
GO

CREATE UNIQUE INDEX UX_Categories_Name ON learning.Categories(Name);
GO

CREATE TABLE learning.Courses
(
    CourseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    CategoryId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(2000) NULL,
    DifficultyLevel NVARCHAR(30) NOT NULL,
    DurationHours INT NOT NULL,
    IsPublished BIT NOT NULL CONSTRAINT DF_Courses_IsPublished DEFAULT 0,
    CreatedUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Courses_CreatedUtc DEFAULT SYSUTCDATETIME(),
    UpdatedUtc DATETIME2(7) NULL,
    CONSTRAINT FK_Courses_Categories FOREIGN KEY (CategoryId) REFERENCES learning.Categories(CategoryId),
    CONSTRAINT CK_Courses_DurationHours CHECK (DurationHours > 0)
);
GO

CREATE INDEX IX_Courses_CategoryId ON learning.Courses(CategoryId);
CREATE INDEX IX_Courses_IsPublished ON learning.Courses(IsPublished);
GO

CREATE TABLE learning.Enrollments
(
    EnrollmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Enrollments PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CourseId UNIQUEIDENTIFIER NOT NULL,
    EnrolledUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Enrollments_EnrolledUtc DEFAULT SYSUTCDATETIME(),
    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Enrollments_Status DEFAULT N'Active',
    CompletedUtc DATETIME2(7) NULL,
    CONSTRAINT FK_Enrollments_Users FOREIGN KEY (UserId) REFERENCES learning.Users(UserId),
    CONSTRAINT FK_Enrollments_Courses FOREIGN KEY (CourseId) REFERENCES learning.Courses(CourseId)
);
GO

CREATE UNIQUE INDEX UX_Enrollments_UserId_CourseId ON learning.Enrollments(UserId, CourseId);
CREATE INDEX IX_Enrollments_CourseId ON learning.Enrollments(CourseId);
GO

CREATE TABLE learning.LearningProgress
(
    LearningProgressId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LearningProgress PRIMARY KEY,
    EnrollmentId UNIQUEIDENTIFIER NOT NULL,
    PercentComplete DECIMAL(5,2) NOT NULL CONSTRAINT DF_LearningProgress_PercentComplete DEFAULT 0,
    LastAccessedUtc DATETIME2(7) NULL,
    Notes NVARCHAR(1000) NULL,
    UpdatedUtc DATETIME2(7) NOT NULL CONSTRAINT DF_LearningProgress_UpdatedUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_LearningProgress_Enrollments FOREIGN KEY (EnrollmentId) REFERENCES learning.Enrollments(EnrollmentId) ON DELETE CASCADE,
    CONSTRAINT CK_LearningProgress_PercentComplete CHECK (PercentComplete >= 0 AND PercentComplete <= 100)
);
GO

CREATE UNIQUE INDEX UX_LearningProgress_EnrollmentId ON learning.LearningProgress(EnrollmentId);
GO
