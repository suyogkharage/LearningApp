USE LearningAppDb;
GO

DECLARE @AdminId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @LearnerId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @CategoryDotNet UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';
DECLARE @CategoryCloud UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';
DECLARE @CourseApi UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555555';
DECLARE @CourseBlazor UNIQUEIDENTIFIER = '66666666-6666-6666-6666-666666666666';
DECLARE @EnrollmentId UNIQUEIDENTIFIER = '77777777-7777-7777-7777-777777777777';

IF NOT EXISTS (SELECT 1 FROM learning.Users WHERE UserId = @AdminId)
BEGIN
    INSERT INTO learning.Users (UserId, Email, FullName, PasswordHash, Role, IsActive)
    VALUES
    (@AdminId, N'admin@learning.local', N'Learning Administrator', N'100000.xu++cBEV/4WZmG10gFM3ig==.VKobJTocUGRmPl9gyq1k91/YAoEAdAgtey0EftvcpRk=', N'Admin', 1),
    (@LearnerId, N'learner@learning.local', N'Demo Learner', N'100000.xu++cBEV/4WZmG10gFM3ig==.VKobJTocUGRmPl9gyq1k91/YAoEAdAgtey0EftvcpRk=', N'Learner', 1);
END

IF NOT EXISTS (SELECT 1 FROM learning.Categories WHERE CategoryId = @CategoryDotNet)
BEGIN
    INSERT INTO learning.Categories (CategoryId, Name, Description)
    VALUES
    (@CategoryDotNet, N'.NET Development', N'Enterprise application development with modern .NET.'),
    (@CategoryCloud, N'Cloud Architecture', N'Cloud-native design, deployment, and operations.');
END

IF NOT EXISTS (SELECT 1 FROM learning.Courses WHERE CourseId = @CourseApi)
BEGIN
    INSERT INTO learning.Courses (CourseId, CategoryId, Title, Description, DifficultyLevel, DurationHours, IsPublished)
    VALUES
    (@CourseApi, @CategoryDotNet, N'Building APIs with .NET 8', N'Clean REST APIs with EF Core, JWT, logging, and validation.', N'Intermediate', 12, 1),
    (@CourseBlazor, @CategoryDotNet, N'Blazor WebAssembly for Teams', N'Build maintainable client applications with Blazor WebAssembly.', N'Beginner', 10, 1);
END

IF NOT EXISTS (SELECT 1 FROM learning.Enrollments WHERE EnrollmentId = @EnrollmentId)
BEGIN
    INSERT INTO learning.Enrollments (EnrollmentId, UserId, CourseId, Status)
    VALUES (@EnrollmentId, @LearnerId, @CourseApi, N'Active');

    INSERT INTO learning.LearningProgress (LearningProgressId, EnrollmentId, PercentComplete, LastAccessedUtc, Notes)
    VALUES ('88888888-8888-8888-8888-888888888888', @EnrollmentId, 35.00, SYSUTCDATETIME(), N'Completed the authentication module.');
END
GO
