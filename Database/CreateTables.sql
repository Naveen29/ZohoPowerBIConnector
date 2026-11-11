

USE ZohoBI;
GO

CREATE TABLE Users
(
    Id INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(200),
    CompanyName NVARCHAR(200),
    Email NVARCHAR(200) UNIQUE,
    Mobile NVARCHAR(50),
    PasswordHash NVARCHAR(MAX),
    IsEmailConfirmed BIT DEFAULT 0,
    EmailConfirmationToken NVARCHAR(MAX),
    EmailConfirmationExpiresUtc DATETIME,
    Role NVARCHAR(100),
    IsActive BIT,
    PasswordResetToken NVARCHAR(MAX),
    PasswordResetExpiresUtc DATETIME,
    LastLoginUtc DATETIME,
    CreatedDate DATETIME,
    UpdatedDate DATETIME
);

CREATE TABLE Plans
(
    Id INT IDENTITY PRIMARY KEY,
    [Name] NVARCHAR(100),
    Price DECIMAL(18,2),
    DurationDays INT,
    Description NVARCHAR(MAX),
    IsActive BIT
);

CREATE TABLE Subscriptions
(
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    PlanId INT NOT NULL,
    StartDate DATETIME NOT NULL,
    ExpiryDate DATETIME NOT NULL,
    IsActive BIT NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (PlanId) REFERENCES Plans(Id)
);

CREATE TABLE ZohoConfigurations
(
    Id INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL,
    ClientId NVARCHAR(500),
    BaseUrl NVARCHAR(500),
    OrganizationId NVARCHAR(200),
    ClientSecret NVARCHAR(MAX),
    RefreshToken NVARCHAR(MAX),
    LastSyncAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT UQ_ZohoConfigurations_UserId UNIQUE (UserId)
);

CREATE TABLE EmailConfig
(
    Id INT IDENTITY PRIMARY KEY,
    SmtpHost NVARCHAR(500),
    SmtpPort INT,
    Username NVARCHAR(500),
    Password NVARCHAR(MAX),
    EnableSSL BIT,
    FromEmail NVARCHAR(500),
    FromName NVARCHAR(200),
    IsActive BIT
);

CREATE TABLE PluginActivityLogs
(
    Id INT IDENTITY PRIMARY KEY,
    UserId INT,
    SyncDate DATETIME,
    Status NVARCHAR(100),
    Message NVARCHAR(MAX),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE ContactMessages
(
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(200),
    Email NVARCHAR(200),
    Message NVARCHAR(MAX),
    CreatedDate DATETIME
);

Create TABLE SyncLog(
    Id INT PRIMARY KEY IDENTITY(1,1),
    ClientId INT,
    EventType VARCHAR(50),
    CreatedAt DATETIME
);
