# 📡 StickIt API Documentation

## Overview

StickIt provides a comprehensive REST API built on ASP.NET Core 10 MVC controllers with Razor Pages. The API supports all core e-commerce functionality with proper authentication, authorization, and error handling.

## 🚀 Quick Start

### Base URLs
- **Development**: `https://localhost:5001`
- **Staging**: `https://stickit-staging.azurewebsites.net`
- **Production**: `https://stickit.example.com`

### Authentication
Authenticated web endpoints use ASP.NET Core Identity session cookies. JWT bearer authentication is not currently registered in the application startup.

```http
GET /UserProfile/Index
Cookie: .AspNetCore.Identity.Application=<session-cookie>
```

## 🔐 Authentication & Authorization

### Authentication Methods
- **Session-based** - Default for web browsers
- **Bearer Token** - Not currently implemented; add JWT bearer registration before documenting token-based API clients
- **Identity Integration** - ASP.NET Core Identity with role-based access

### Authorization Levels
| Role | Permissions | Controllers |
|------|------------|-------------|
| **Customer** | Basic user operations | UserProfile, UserAddress, UserReview |
| **Staff** | Order management | Customer + Order management |
| **Manager** | Advanced reporting | Staff + Analytics access |
| **Admin** | Full system access | All controllers including AdminSystem |

### Example Authentication Flow
```csharp
// Login endpoint
POST /Identity/Account/Login
{
  "Email": "user@example.com",
  "Password": "SecurePassword123!",
  "RememberMe": true
}
```

## 👤 User Management API

### User Profile Controllers

#### UserProfileController
**Base Route**: `/UserProfile`

##### Get Dashboard
```http
GET /UserProfile/Index
Authorization: Required
```

**Response:**
```json
{
  "profile": {
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "hasAvatar": true
  },
  "wishListCount": 5,
  "activeOrders": 2,
  "recentOrders": [...]
}
```

##### Update Profile
```http
POST /UserProfile/Edit
Authorization: Required
Content-Type: application/json

{
  "profile": {
    "firstName": "John",
    "lastName": "Doe"
  }
}
```

##### Upload Avatar
```http
POST /UserProfile/UploadAvatar
Authorization: Required
Content-Type: multipart/form-data

file: <image-file>
```

**Validation:**
- Max file size: 10MB
- Supported formats: JPEG, PNG, GIF, WebP

##### Dashboard Sections (AJAX)
```http
GET /UserProfile/WishlistSection?page=1&sort=date_desc
GET /UserProfile/ActiveOrdersSection?page=1&sort=date_desc
GET /UserProfile/OrderHistorySection?page=1&sort=date_desc
```

#### UserAddressController
**Base Route**: `/UserAddress`

##### List Addresses
```http
GET /UserAddress/Index
Authorization: Required
```

**Response:**
```json
[
  {
    "contactId": 1,
    "firstName": "John",
    "lastName": "Doe",
    "street": "123 Main St",
    "city": "Toronto",
    "province": "ON",
    "postCode": "M5V 3A5",
    "country": "Canada",
    "isDefault": true
  }
]
```

##### Create Address
```http
POST /UserAddress/Create
Authorization: Required
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe", 
  "phoneNumber": "+1-416-555-0123",
  "street": "123 Main St",
  "city": "Toronto",
  "province": "ON",
  "postCode": "M5V 3A5",
  "country": "Canada",
  "isDefault": false
}
```

##### Update Address
```http
POST /UserAddress/Edit/{id}
Authorization: Required
```

##### Delete Address
```http
POST /UserAddress/Delete/{id}
Authorization: Required
```

##### Set Default Address
```http
POST /UserAddress/SetDefault/{id}
Authorization: Required
```

#### UserReviewController
**Base Route**: `/UserReview`

##### My Ratings
```http
GET /UserReview/MyRatings?sort=purchase_desc
Authorization: Required
```

**Sort Options:**
- `purchase_desc` (default)
- `purchase_asc`
- `name_asc`
- `name_desc`
- `rating_high`
- `rating_low`

##### Submit Product Rating
```http
POST /UserReview/SubmitRating
Authorization: Required
Content-Type: application/json

{
  "productId": 123,
  "orderItemId": 456,
  "rating": 5,
  "description": "Excellent product quality!"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Rating submitted successfully"
}
```

##### Store Review
```http
GET /UserReview/StoreReview
Authorization: Optional (redirects to login if not authenticated)
```

```http
POST /UserReview/StoreReview
Authorization: Required
Content-Type: application/json

{
  "title": "Great Shopping Experience",
  "rating": 5,
  "description": "Fast delivery and quality products!"
}
```

## 🛠️ Admin API

### Admin User Management

#### AdminUserController
**Base Route**: `/AdminUser`
**Authorization**: Admin role required

##### List Users
```http
GET /AdminUser/Index?search={email}&roleFilter={role}&page={page}
Authorization: Admin
```

**Parameters:**
- `search` - Email substring filter
- `roleFilter` - Role filter (Admin, Manager, Staff, Customer, All)
- `page` - Page number (default: 1)

**Response:**
```json
{
  "users": [
    {
      "id": "user-guid",
      "email": "user@example.com",
      "roles": ["Customer"]
    }
  ],
  "currentPage": 1,
  "totalPages": 10,
  "hasNext": true,
  "hasPrevious": false
}
```

##### User Details
```http
GET /AdminUser/Details/{userId}
Authorization: Admin
```

**Response:**
```json
{
  "identityUser": {
    "id": "user-guid",
    "email": "user@example.com"
  },
  "profile": {
    "firstName": "John",
    "lastName": "Doe"
  },
  "roles": ["Customer"],
  "recentOrders": [...],
  "contact": {...}
}
```

##### Role Management
```http
POST /AdminUser/AddRole
Authorization: Admin
Content-Type: application/json

{
  "userId": "user-guid",
  "role": "Manager"
}
```

```http
POST /AdminUser/RemoveRole
Authorization: Admin
Content-Type: application/json

{
  "userId": "user-guid", 
  "role": "Manager"
}
```

##### Available Roles (AJAX)
```http
GET /AdminUser/AvailableRoles/{userId}
Authorization: Admin
```

##### User Statistics
```http
GET /AdminUser/Statistics
Authorization: Admin
```

### Admin Analytics

#### AdminAnalyticsController
**Base Route**: `/AdminAnalytics`
**Authorization**: Admin role required

##### Dashboard Metrics
```http
GET /AdminAnalytics/Index
Authorization: Admin
```

**Response:**
```json
{
  "weeklyTotalOrders": 147,
  "monthlyTotalOrders": 632,
  "stockUpCount": 89,
  "stockDownCount": 23,
  "topProducts": [
    {
      "productName": "Premium Vinyl Stickers",
      "unitsSold": 342,
      "revenue": 6840.00
    }
  ]
}
```

##### Sales Analytics
```http
GET /AdminAnalytics/Sales
Authorization: Admin
```

**Response:**
```json
{
  "weeklyGrossSales": 15420.50,
  "monthlyGrossSales": 67890.25,
  "weeklyLabels": ["Mon 17", "Tue 18", "Wed 19", "Thu 20", "Fri 21", "Sat 22", "Sun 23"],
  "weeklySalesData": [2100.00, 2340.50, 1890.25, 2567.75, 3210.00, 1876.50, 1435.50],
  "monthlyLabels": ["Jan 2026", "Feb 2026", "Mar 2026"],
  "monthlySalesData": [45230.75, 52150.25, 67890.25],
  "topProducts": [...]
}
```

##### Product Analytics
```http
GET /AdminAnalytics/Products
Authorization: Admin
```

##### Export Sales Data
```http
GET /AdminAnalytics/ExportSalesData?startDate=2026-01-01&endDate=2026-03-31
Authorization: Admin
```

**Response**: CSV file download

### Admin System Management

#### AdminSystemController
**Base Route**: `/AdminSystem`
**Authorization**: Admin role required

##### Search Index Management
```http
POST /AdminSystem/ReindexSearch
Authorization: Admin
Content-Type: application/json

{
  "reason": "Monthly index optimization"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Search index rebuilt successfully"
}
```

##### Search Health Status
```http
GET /AdminSystem/SearchHealth
Authorization: Admin
```

**Response:**
```json
{
  "success": true,
  "totalProducts": 1250,
  "indexedProducts": 1250,
  "indexCoverage": 1.0,
  "backgroundService": {
    "running": true,
    "lastRun": "2026-03-24T14:30:00Z",
    "lastDuration": 1250.5
  }
}
```

##### Cache Management
```http
GET /AdminSystem/CacheStats
Authorization: Admin
```

```http
POST /AdminSystem/ClearCache
Authorization: Admin
Content-Type: application/json

{
  "reason": "Performance optimization after bulk updates"
}
```

**Response:**
```json
{
  "success": true,
  "cleared": 47,
  "message": "Successfully cleared 47 cache entries"
}
```

##### System Health Check
```http
GET /AdminSystem/HealthCheck
Authorization: Admin
```

**Response:**
```json
{
  "success": true,
  "health": {
    "database": {
      "status": "Healthy",
      "canConnect": true,
      "productCount": 1250,
      "lastChecked": "2026-03-24T14:35:00Z"
    },
    "cache": {
      "status": "Healthy", 
      "registeredKeys": 47,
      "lastChecked": "2026-03-24T14:35:00Z"
    },
    "search": {
      "status": "Healthy",
      "totalProducts": 1250,
      "lastChecked": "2026-03-24T14:35:00Z"
    }
  }
}
```

##### Background Services Status
```http
GET /AdminSystem/BackgroundServices
Authorization: Admin
```

## 🛒 E-commerce API

### Product Operations
```http
GET /Product/Index?search={query}&category={id}&sort={option}&page={page}
GET /Product/Details/{id}
GET /Product/Search?q={fuzzy-query}
```

### Cart Operations
```http
GET /Cart/Index
POST /Cart/AddItem
POST /Cart/UpdateQuantity
POST /Cart/RemoveItem
POST /Cart/Clear
```

### Order Operations  
```http
GET /Order/Index
GET /Order/Details/{id}
POST /Order/Create
GET /Order/Checkout
POST /Order/ProcessPayment
```

## 📊 Health & Monitoring

### Health Checks
```http
GET /health
```

**Response:**
```json
{
  "status": "Healthy",
  "results": {
    "database": {
      "status": "Healthy",
      "description": "Database connection successful"
    },
    "memory": {
      "status": "Healthy", 
      "description": "Memory usage within limits"
    }
  },
  "totalDuration": "00:00:00.1234567"
}
```

### Metrics Endpoint
```http
GET /metrics
```

**Response**: Prometheus metrics format

## 🚫 Error Handling

### Standard Error Response
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more validation errors occurred.",
    "details": {
      "Email": ["The Email field is required."],
      "Password": ["Password must be at least 8 characters."]
    },
    "timestamp": "2026-03-24T14:35:00Z",
    "traceId": "0HN7KOJKL0J0A"
  }
}
```

### HTTP Status Codes
- **200 OK** - Successful request
- **201 Created** - Resource created successfully
- **400 Bad Request** - Invalid request data
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Resource not found
- **429 Too Many Requests** - Rate limit exceeded
- **500 Internal Server Error** - Server error

### Error Categories
- **Validation Errors** - Input validation failures
- **Business Errors** - Business rule violations  
- **Security Errors** - Authentication/authorization failures
- **System Errors** - Infrastructure and unexpected errors

## 🔄 Rate Limiting

### Admin Endpoints
- **Rate**: 60 requests per minute
- **Burst**: 10 requests per 10 seconds
- **Policy**: `AdminRateLimit`

### User Endpoints  
- **Rate**: 120 requests per minute
- **Burst**: 20 requests per 10 seconds
- **Policy**: `UserRateLimit`

### Public Endpoints
- **Rate**: 300 requests per minute
- **Burst**: 50 requests per 10 seconds
- **Policy**: `PublicRateLimit`

## 📡 CORS Configuration

### Allowed Origins
- Development: `https://localhost:*`
- Staging: `https://*.azurewebsites.net`
- Production: `https://elkh.example.com`

### Allowed Methods
- GET, POST, PUT, DELETE, OPTIONS

### Allowed Headers
- Authorization, Content-Type, Accept, X-Requested-With

## 📝 Request/Response Examples

### Complete User Registration Flow
```http
POST /Identity/Account/Register
Content-Type: application/json

{
  "Email": "newuser@example.com",
  "Password": "SecurePassword123!",
  "ConfirmPassword": "SecurePassword123!",
  "FirstName": "Jane",
  "LastName": "Smith"
}
```

### Complete Order Flow
```http
# 1. Add items to cart
POST /Cart/AddItem
{
  "productId": 123,
  "quantity": 2
}

# 2. View cart
GET /Cart/Index

# 3. Proceed to checkout
GET /Order/Checkout

# 4. Process payment
POST /Order/ProcessPayment
{
  "contactId": 1,
  "paymentMethod": "CreditCard",
  "cardToken": "tok_visa_debit"
}
```

## 🧪 Testing the API

### Using cURL
```bash
# Get user dashboard
curl -X GET "https://localhost:5001/UserProfile/Index" \
  -H "Cookie: .AspNetCore.Identity.Application=<session-cookie>"

# Submit a product rating
curl -X POST "https://localhost:5001/UserReview/SubmitRating" \
  -H "Content-Type: application/json" \
  -H "Cookie: .AspNetCore.Identity.Application=<session-cookie>" \
  -d '{"productId": 123, "orderItemId": 456, "rating": 5, "description": "Great product!"}'
```

### Using Postman
1. Import the environment variables
2. Use the authentication flow to get session cookies
3. Test individual endpoints with proper authorization headers

### API Client Examples
```csharp
// C# HttpClient example
using var client = new HttpClient();
client.BaseAddress = new Uri("https://localhost:5001/");

var response = await client.GetAsync("UserProfile/Index");
var content = await response.Content.ReadAsStringAsync();
```

## 📚 Related Documentation

- **[Architecture Guide](ARCHITECTURE.md)** - System design and patterns
- **[Deployment Guide](DEPLOYMENT.md)** - Docker and Azure deployment  
- **[Contributing Guidelines](CONTRIBUTING.md)** - Development workflow
- **[Testing Guide](../ELKH.Tests/README.md)** - API testing approaches

---

*For additional API questions or support, please refer to the [GitHub Issues](https://github.com/Velyene/StickIt/issues) or check the health endpoint for system status.*