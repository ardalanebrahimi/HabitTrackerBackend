# Google Play Billing Integration Setup Guide

## Overview
This guide explains how to configure Google Play billing verification for your Habit Tracker backend API.

## Prerequisites
1. Google Play Console access
2. Google Cloud Console access
3. Service account with Google Play Developer API access

## Step 1: Google Cloud Console Setup

### 1.1 Create a Service Account
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select or create a project
3. Navigate to "IAM & Admin" → "Service Accounts"
4. Click "Create Service Account"
5. Fill in the details:
   - Name: `habit-tracker-play-billing`
   - Description: `Service account for Google Play billing verification`

### 1.2 Generate Service Account Key
1. Click on the created service account
2. Go to "Keys" tab
3. Click "Add Key" → "Create new key"
4. Select "JSON" format
5. Download the JSON key file
6. **Keep this file secure and never commit it to version control!**

### 1.3 Enable Google Play Developer API
1. In Google Cloud Console, navigate to "APIs & Services" → "Library"
2. Search for "Google Play Developer API"
3. Click on it and enable the API

## Step 2: Google Play Console Setup

### 2.1 Link Service Account
1. Go to [Google Play Console](https://play.google.com/console/)
2. Select your app
3. Go to "Setup" → "API access"
4. Click "Link Google Cloud project"
5. Select your Google Cloud project
6. Grant access to the service account you created

### 2.2 Configure Permissions
1. In the "Service accounts" section, find your service account
2. Click "Grant Access"
3. Set permissions:
   - **Financial data**: View only
   - **Order management**: View only

## Step 3: Backend Configuration

### 3.1 Configure appsettings.json
```json
{
  "GooglePlayBilling": {
    "ServiceAccountJsonPath": "path/to/your/service-account-key.json",
    "ApplicationName": "HabitTracker",
    "PackageName": "com.yourcompany.habittracker"
  }
}
```

### 3.2 Alternative: Use JSON Content Directly
For production deployments, you might want to use the JSON content as an environment variable:

```json
{
  "GooglePlayBilling": {
    "ServiceAccountJson": "{\\"type\\":\\"service_account\\",\\"project_id\\":\\"your-project\\",\\"private_key_id\\":\\"...\\",...}",
    "ApplicationName": "HabitTracker",
    "PackageName": "com.yourcompany.habittracker"
  }
}
```

### 3.3 Environment Variables (Recommended for Production)
```bash
GOOGLEPLAYBILLING__SERVICEACCOUNTJSON='{"type":"service_account","project_id":"your-project",...}'
GOOGLEPLAYBILLING__APPLICATIONNAME="HabitTracker"
GOOGLEPLAYBILLING__PACKAGENAME="com.yourcompany.habittracker"
```

## Step 4: Product Configuration

### 4.1 In-App Products (Token Packs)
1. Go to Google Play Console → "Monetize" → "Products" → "In-app products"
2. Create products with IDs matching your backend configuration:
   - `tokens_100` - 100 tokens pack
   - `tokens_500` - 500 tokens pack
   - `tokens_1000` - 1000 tokens pack

### 4.2 Subscriptions
1. Go to Google Play Console → "Monetize" → "Products" → "Subscriptions"
2. Create subscriptions with IDs matching your backend configuration:
   - `premium_monthly` - Monthly premium subscription
   - `premium_yearly` - Yearly premium subscription

## Step 5: Testing

### 5.1 Test Accounts
1. In Google Play Console, go to "Setup" → "License testing"
2. Add test accounts (Google accounts)
3. These accounts can make test purchases without being charged

### 5.2 Test Purchase Flow
1. Install your app on a test device
2. Make a test purchase
3. Verify the purchase is processed correctly in your backend logs

## Step 6: Security Considerations

### 6.1 Service Account Key Security
- Never commit the JSON key file to version control
- Use environment variables or secure key management in production
- Rotate keys regularly
- Limit service account permissions to minimum required

### 6.2 Purchase Verification
- Always verify purchases server-side
- Check for duplicate purchase tokens
- Implement proper error handling
- Log all verification attempts for debugging

## API Endpoints

### Verify Token Purchase
```
POST /api/payments/verify-token-purchase
{
  "purchaseToken": "purchase_token_from_google_play",
  "productId": "tokens_100",
  "orderId": "order_id_from_google_play",
  "tokenAmount": 100,
  "price": 0.99
}
```

### Verify Subscription
```
POST /api/payments/verify-subscription
{
  "purchaseToken": "purchase_token_from_google_play",
  "productId": "premium_monthly",
  "orderId": "order_id_from_google_play",
  "purchaseType": 1
}
```

### Restore Purchases
```
POST /api/payments/restore-purchases
```

## Troubleshooting

### Common Issues
1. **"Service account not found"**: Ensure the service account is properly linked in Google Play Console
2. **"Invalid purchase token"**: Check if the token is valid and not already consumed
3. **"Insufficient permissions"**: Verify the service account has the correct permissions
4. **"Package name mismatch"**: Ensure the package name in configuration matches your app

### Debugging
- Enable detailed logging in your backend
- Check Google Play Console for transaction details
- Verify service account permissions
- Test with license testing accounts first

## Production Deployment

### Environment Variables
```bash
# Service account JSON (base64 encoded for some platforms)
GOOGLEPLAYBILLING__SERVICEACCOUNTJSON="..."

# Application settings
GOOGLEPLAYBILLING__APPLICATIONNAME="HabitTracker"
GOOGLEPLAYBILLING__PACKAGENAME="com.yourcompany.habittracker"
```

### Security Checklist
- [ ] Service account key is not in version control
- [ ] Permissions are minimal (View only for financial data)
- [ ] Purchase tokens are validated server-side
- [ ] Duplicate purchases are prevented
- [ ] Error handling is implemented
- [ ] Logging is configured for debugging

## Support
For issues with this integration, check:
1. Google Play Console transaction logs
2. Your backend application logs
3. Google Play Developer API documentation
4. Stack Overflow for common issues

---

**Note**: This implementation handles both consumable products (token packs) and subscriptions. Make sure to test thoroughly before production deployment.
