using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HabitTrackerBackend.Services
{
    public class GooglePlayBillingService : IGooglePlayBillingService
    {
        private readonly GooglePlayBillingOptions _options;
        private readonly ILogger<GooglePlayBillingService> _logger;
        private AndroidPublisherService? _androidPublisherService;

        public GooglePlayBillingService(IOptions<GooglePlayBillingOptions> options, ILogger<GooglePlayBillingService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        private async Task<AndroidPublisherService> GetAndroidPublisherServiceAsync()
        {
            if (_androidPublisherService != null)
                return _androidPublisherService;

            try
            {
                GoogleCredential credential;
                
                if (!string.IsNullOrEmpty(_options.ServiceAccountJsonPath))
                {
                    // Use service account key file
                    credential = GoogleCredential.FromFile(_options.ServiceAccountJsonPath)
                        .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
                }
                else if (!string.IsNullOrEmpty(_options.ServiceAccountJson))
                {
                    // Use service account key from configuration
                    credential = GoogleCredential.FromJson(_options.ServiceAccountJson)
                        .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
                }
                else
                {
                    throw new InvalidOperationException("Google Play service account credentials not configured");
                }

                _androidPublisherService = new AndroidPublisherService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = _options.ApplicationName ?? "HabitTracker"
                });

                return _androidPublisherService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Play Android Publisher service");
                throw;
            }
        }

        public async Task<ProductPurchase> VerifyProductPurchaseAsync(string packageName, string productId, string purchaseToken)
        {
            try
            {
                var service = await GetAndroidPublisherServiceAsync();
                var request = service.Purchases.Products.Get(packageName, productId, purchaseToken);
                var response = await request.ExecuteAsync();
                
                _logger.LogInformation("Product purchase verified: {ProductId}, Status: {Status}", 
                    productId, response.PurchaseState);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify product purchase: {ProductId}, Token: {Token}", 
                    productId, purchaseToken);
                throw;
            }
        }

        public async Task<SubscriptionPurchase> VerifySubscriptionPurchaseAsync(string packageName, string subscriptionId, string purchaseToken)
        {
            try
            {
                var service = await GetAndroidPublisherServiceAsync();
                var request = service.Purchases.Subscriptions.Get(packageName, subscriptionId, purchaseToken);
                var response = await request.ExecuteAsync();
                
                _logger.LogInformation("Subscription purchase verified: {SubscriptionId}, Status: {Status}", 
                    subscriptionId, response.PaymentState);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify subscription purchase: {SubscriptionId}, Token: {Token}", 
                    subscriptionId, purchaseToken);
                throw;
            }
        }

        public async Task<bool> IsProductPurchaseValidAsync(string packageName, string productId, string purchaseToken)
        {
            try
            {
                var productPurchase = await VerifyProductPurchaseAsync(packageName, productId, purchaseToken);
                
                // Check if purchase is valid
                // PurchaseState: 0 = Purchased, 1 = Cancelled, 2 = Pending
                // ConsumptionState: 0 = Yet to be consumed, 1 = Consumed
                return productPurchase.PurchaseState == 0 && // Purchased
                       productPurchase.ConsumptionState == 0; // Not yet consumed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product purchase: {ProductId}", productId);
                return false;
            }
        }

        public async Task<bool> IsSubscriptionPurchaseValidAsync(string packageName, string subscriptionId, string purchaseToken)
        {
            try
            {
                var subscriptionPurchase = await VerifySubscriptionPurchaseAsync(packageName, subscriptionId, purchaseToken);
                
                // Check if subscription is valid
                // PaymentState: 0 = Payment pending, 1 = Payment received, 2 = Free trial, 3 = Pending deferred upgrade/downgrade
                // AutoRenewing: true if subscription will auto-renew
                var isActive = subscriptionPurchase.PaymentState == 1 || // Payment received
                              subscriptionPurchase.PaymentState == 2;   // Free trial
                
                var isNotExpired = subscriptionPurchase.ExpiryTimeMillis > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                return isActive && isNotExpired;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating subscription purchase: {SubscriptionId}", subscriptionId);
                return false;
            }
        }

        public async Task<List<SubscriptionPurchase>> GetUserSubscriptionsAsync(string packageName, string userId)
        {
            try
            {
                var service = await GetAndroidPublisherServiceAsync();
                var subscriptions = new List<SubscriptionPurchase>();
                
                // Note: This is a simplified implementation
                // In reality, you'd need to store the mapping between your user IDs and Google Play purchase tokens
                // This method would typically query your database for purchase tokens associated with the user
                // and then verify each one with Google Play
                
                _logger.LogInformation("Retrieved subscriptions for user: {UserId}", userId);
                
                return subscriptions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user subscriptions: {UserId}", userId);
                throw;
            }
        }

        public void Dispose()
        {
            _androidPublisherService?.Dispose();
        }
    }

    public class GooglePlayBillingOptions
    {
        public string? ServiceAccountJsonPath { get; set; }
        public string? ServiceAccountJson { get; set; }
        public string? ApplicationName { get; set; }
        public string? PackageName { get; set; }
    }
}
