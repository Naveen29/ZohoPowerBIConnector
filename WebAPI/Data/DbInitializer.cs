using Microsoft.AspNetCore.Identity;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Data;

public static class DbInitializer
{
    private const string SuperAdminEmail = "admin@zoho.com";
    private const string SuperAdminPassword = "Admin@12345";
    private const string SuperAdminFullName = "Super Admin";
    private const string SuperAdminRole = "Admin";

    public static void Seed(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Seed Super Admin
        if (!dbContext.Users.Any(u => u.Email == SuperAdminEmail))
        {
            var superAdmin = new User
            {
                Email = SuperAdminEmail,
                FullName = SuperAdminFullName,
                Role = SuperAdminRole,
                IsActive = true
            };

            var passwordHasher = new PasswordHasher<User>();
            superAdmin.PasswordHash = passwordHasher.HashPassword(superAdmin, SuperAdminPassword);
            dbContext.Users.Add(superAdmin);
            dbContext.SaveChanges();
        }

        // Seed Plans
        if (!dbContext.Plans.Any())
        {
            var plans = new[] {
                new Plan { Name = "Free", Description = "Free tier", Price = 0m, DurationDays = 365, IsActive = true },
                new Plan { Name = "Pro", Description = "Pro tier", Price = 49.99m, DurationDays = 30, IsActive = true },
                new Plan { Name = "Business", Description = "Business tier", Price = 199.99m, DurationDays = 30, IsActive = true }
            };
            dbContext.Plans.AddRange(plans);
            dbContext.SaveChanges();
        }

        // Seed sample users (non-admin)
        if (!dbContext.Users.Any(u => u.Email == "alice@example.com"))
        {
            var passwordHasher = new PasswordHasher<User>();
            var alice = new User { Email = "alice@example.com", FullName = "Alice Tester", Role = "User", IsActive = true };
            alice.PasswordHash = passwordHasher.HashPassword(alice, "Password123!");

            var bob = new User { Email = "bob@example.com", FullName = "Bob Sample", Role = "User", IsActive = false };
            bob.PasswordHash = passwordHasher.HashPassword(bob, "Password123!");

            dbContext.Users.AddRange(alice, bob);
            dbContext.SaveChanges();

            // Assign a subscription to Alice (Pro)
            var proPlan = dbContext.Plans.FirstOrDefault(p => p.Name == "Pro");
            if (proPlan is not null)
            {
                var sub = new Subscription
                {
                    UserId = alice.Id,
                    PlanId = proPlan.Id,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(proPlan.DurationDays),
                    IsActive = true
                };
                dbContext.Subscriptions.Add(sub);
                dbContext.SaveChanges();
            }
        }
    }
}
