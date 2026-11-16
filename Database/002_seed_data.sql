USE LearningAppDb;
GO

DECLARE @CategoryDotNet UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';
DECLARE @CategoryCloud UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';
DECLARE @CourseApi UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555555';
DECLARE @CourseBlazor UNIQUEIDENTIFIER = '66666666-6666-6666-6666-666666666666';
DECLARE @AdminUser UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

IF NOT EXISTS (SELECT 1 FROM learning.Users WHERE Email = N'admin@learningapp.local')
BEGIN
    -- Bootstrap admin for development/demo deployments. Password: Admin@12345
    INSERT INTO learning.Users (UserId, Email, FullName, PasswordHash, Role, IsActive)
    VALUES
    (
        @AdminUser,
        N'admin@learningapp.local',
        N'Learning App Admin',
        N'100000.zOrb8EsntadcgiKDoU3dgg==.AXYKAcWCXsKiNF4VC544iezMIl3tf9LOTG/s+oOhGzI=',
        N'Admin',
        1
    );
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
GO
