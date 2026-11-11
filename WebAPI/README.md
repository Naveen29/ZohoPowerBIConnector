# WebAPI

This folder contains the ASP.NET Core 8 Web API project scaffold for the Zoho Power BI Connector portal.

## Current scaffold

- `ZohoPowerBIConnector.WebAPI.csproj` targets .NET 8.0
- `Program.cs` configures controllers, Swagger, EF Core, and JWT authentication stubs
- `appsettings.json` includes connection string and JWT placeholders
- `Models/` includes entity models and `ApplicationDbContext`
- `Controllers/` includes a health check controller, auth controller with login/register/forgot-password/reset-password, dashboard controller with summary API, Zoho controller for configuration and validation, and an admin controller for user and subscription management (admin role required)

## Goals

- Implement JWT authentication
- Expose user registration, login, dashboard, Zoho configuration, subscription, and plugin validation APIs
- Protect secrets and support HTTPS
