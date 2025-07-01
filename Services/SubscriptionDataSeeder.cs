using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public static class SubscriptionDataSeeder
{
    public static async Task SeedSubscriptionPlansAsync(AppDbContext context)
    {
        if (await context.SubscriptionPlans.AnyAsync())
        {
            return; // Plans already seeded
        }

        var plans = new List<SubscriptionPlan>
        {
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                PlanName = "premium_monthly",
                DisplayName = "Premium Monthly",
                Price = 4.99m,
                Currency = "USD",
                DurationMonths = 1,
                TokensIncluded = 50,
                HabitLimit = 50,
                Features = JsonSerializer.Serialize(new List<string>
                {
                    "50 habits max",
                    "Advanced analytics",
                    "50 monthly tokens",
                    "Priority AI generation",
                    "Custom themes",
                    "Export data"
                }),
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                PlanName = "premium_yearly",
                DisplayName = "Premium Yearly",
                Price = 49.99m,
                Currency = "USD",
                DurationMonths = 12,
                TokensIncluded = 600,
                HabitLimit = 100,
                Features = JsonSerializer.Serialize(new List<string>
                {
                    "100 habits max",
                    "Advanced analytics",
                    "600 yearly tokens",
                    "Priority AI generation",
                    "Custom themes",
                    "Export data",
                    "Premium support"
                }),
                IsActive = true
            }
        };

        context.SubscriptionPlans.AddRange(plans);
        await context.SaveChangesAsync();
    }
}

public static class TokenProductCatalog
{
    public static readonly Dictionary<string, (int tokens, decimal price)> TokenPacks = new()
    {
        { "tokens_10", (10, 0.99m) },
        { "tokens_25", (25, 1.99m) },
        { "tokens_50", (50, 3.99m) },
        { "tokens_100", (100, 6.99m) }
    };
}
