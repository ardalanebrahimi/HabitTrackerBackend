using Google.Apis.AndroidPublisher.v3.Data;

namespace HabitTrackerBackend.Services
{
    public interface IGooglePlayBillingService
    {
        Task<ProductPurchase> VerifyProductPurchaseAsync(string packageName, string productId, string purchaseToken);
        Task<SubscriptionPurchase> VerifySubscriptionPurchaseAsync(string packageName, string subscriptionId, string purchaseToken);
        Task<bool> IsProductPurchaseValidAsync(string packageName, string productId, string purchaseToken);
        Task<bool> IsSubscriptionPurchaseValidAsync(string packageName, string subscriptionId, string purchaseToken);
        Task<List<SubscriptionPurchase>> GetUserSubscriptionsAsync(string packageName, string userId);
    }
}
