USE ZohoBI;
GO

-- Seed Plans
IF NOT EXISTS (SELECT 1 FROM Plans WHERE [Name] = 'Free')
BEGIN
    INSERT INTO Plans ([Name], Price, DurationDays, Description, IsActive)
    VALUES
    ('Free', 0.00, 10, 'Free tier', 1),
    ('Pro', 49.99, 30, 'Pro tier', 1),
    ('Business', 199.99, 365, 'Business tier', 1);
END
GO

-- Seed Users (passwords should be hashed by the application)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@zoho.com')
BEGIN
    INSERT INTO Users (FullName, Email, Role, IsActive, IsEmailConfirmed, CreatedDate)
    VALUES ('Super Admin', 'admin@zoho.com', 'Admin', 1, 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'alice@example.com')
BEGIN
    INSERT INTO Users (FullName, Email, Role, IsActive, IsEmailConfirmed, CreatedDate)
    VALUES ('Alice Tester', 'alice@example.com', 'User', 1, 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'bob@example.com')
BEGIN
    INSERT INTO Users (FullName, Email, Role, IsActive, IsEmailConfirmed, CreatedDate)
    VALUES ('Bob Sample', 'bob@example.com', 'User', 0, 1, GETUTCDATE());
END
GO

-- Assign Pro subscription to Alice
DECLARE @aliceId INT = (SELECT Id FROM Users WHERE Email = 'alice@example.com');
DECLARE @proPlanId INT = (SELECT Id FROM Plans WHERE [Name] = 'Pro');
IF @aliceId IS NOT NULL AND @proPlanId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Subscriptions WHERE UserId = @aliceId AND PlanId = @proPlanId)
    BEGIN
        INSERT INTO Subscriptions (UserId, PlanId, StartDate, ExpiryDate, IsActive)
        VALUES (@aliceId, @proPlanId, GETUTCDATE(), DATEADD(day, (SELECT DurationDays FROM Plans WHERE Id = @proPlanId), GETUTCDATE()), 1);
    END
END
GO

-- Stored procedure to assign a plan to a user
IF OBJECT_ID('dbo.AssignPlanToUser', 'P') IS NOT NULL
    DROP PROCEDURE dbo.AssignPlanToUser;
GO
CREATE PROCEDURE dbo.AssignPlanToUser
    @UserId INT,
    @PlanId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @duration INT = (SELECT DurationDays FROM Plans WHERE Id = @PlanId);
    IF @duration IS NULL
    BEGIN
        RAISERROR('Plan not found', 16, 1);
        RETURN;
    END
    INSERT INTO Subscriptions (UserId, PlanId, StartDate, ExpiryDate, IsActive)
    VALUES (@UserId, @PlanId, GETUTCDATE(), DATEADD(day, @duration, GETUTCDATE()), 1);
END
GO

-- Stored procedure to extend a subscription
IF OBJECT_ID('dbo.ExtendSubscription', 'P') IS NOT NULL
    DROP PROCEDURE dbo.ExtendSubscription;
GO
CREATE PROCEDURE dbo.ExtendSubscription
    @SubscriptionId INT,
    @ExtraDays INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Subscriptions
    SET ExpiryDate = DATEADD(day, @ExtraDays, ExpiryDate)
    WHERE Id = @SubscriptionId;
END
GO
