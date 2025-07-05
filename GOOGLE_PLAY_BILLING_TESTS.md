# Testing Google Play Billing Integration

## Test Endpoints

### 1. Verify Token Purchase
```http
POST {{apiUrl}}/payments/verify-token-purchase
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{
  "purchaseToken": "test_purchase_token_123",
  "productId": "tokens_100",
  "orderId": "TEST_ORDER_123",
  "tokenAmount": 100,
  "price": 0.99
}
```

### 2. Verify Subscription
```http
POST {{apiUrl}}/payments/verify-subscription
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{
  "purchaseToken": "test_subscription_token_456",
  "productId": "premium_monthly",
  "orderId": "TEST_SUB_ORDER_456",
  "purchaseType": 1
}
```

### 3. Get Subscription Plans
```http
GET {{apiUrl}}/payments/subscription-plans
Authorization: Bearer {{accessToken}}
```

### 4. Cancel Subscription
```http
POST {{apiUrl}}/payments/cancel-subscription
Authorization: Bearer {{accessToken}}
```

### 5. Restore Purchases
```http
POST {{apiUrl}}/payments/restore-purchases
Authorization: Bearer {{accessToken}}
```

## Test Data Setup

### Add Test Subscription Plans
```sql
INSERT INTO subscription_plans (id, plan_name, display_name, price, currency, duration_months, tokens_included, habit_limit, features, is_active, created_at) VALUES
('11111111-1111-1111-1111-111111111111', 'premium_monthly', 'Premium Monthly', 4.99, 'USD', 1, 100, 50, '["Unlimited habits", "AI suggestions", "Priority support"]', true, NOW()),
('22222222-2222-2222-2222-222222222222', 'premium_yearly', 'Premium Yearly', 49.99, 'USD', 12, 1200, 50, '["Unlimited habits", "AI suggestions", "Priority support", "2 months free"]', true, NOW());
```

## Expected Responses

### Successful Token Purchase
```json
{
  "tokenBalance": 150,
  "subscriptionTier": "free",
  "subscriptionExpiresAt": null,
  "habitLimit": 5,
  "habitsCreated": 2,
  "canCreateHabits": true,
  "nextTokenRefresh": null
}
```

### Successful Subscription Verification
```json
{
  "subscriptionTier": "premium_monthly",
  "expiresAt": "2024-02-15T10:30:00Z",
  "isActive": true,
  "autoRenew": true,
  "features": ["Unlimited habits", "AI suggestions", "Priority support"],
  "habitLimit": 50,
  "tokensIncluded": 100
}
```

### Error Response
```json
{
  "message": "Invalid purchase token"
}
```

## Testing Notes

1. **Development Mode**: The service will return `false` for purchase verification if Google Play credentials are not configured
2. **Test Accounts**: Use Google Play Console license testing accounts for real purchase testing
3. **Duplicate Prevention**: The system prevents processing the same purchase token twice
4. **Logging**: Check application logs for detailed verification information

## Configuration for Testing

### appsettings.Development.json
```json
{
  "GooglePlayBilling": {
    "ServiceAccountJsonPath": "path/to/test-service-account.json",
    "ApplicationName": "HabitTracker-Dev",
    "PackageName": "com.yourcompany.habittracker.dev"
  }
}
```

### Environment Variables for CI/CD
```bash
export GOOGLEPLAYBILLING__SERVICEACCOUNTJSON='{"type":"service_account",...}'
export GOOGLEPLAYBILLING__APPLICATIONNAME="HabitTracker"
export GOOGLEPLAYBILLING__PACKAGENAME="com.yourcompany.habittracker"
```
