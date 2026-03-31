# Controller Decomposition Route Mapping

## Original vs New Controller Routes

This document maps the old routes to the new decomposed controller structure to ensure no breaking changes.

### UserController → Multiple Controllers

| **Original Route** | **New Route** | **Controller** | **Action** |
|-------------------|---------------|----------------|------------|
| `/User/Index` | `/UserProfile/Index` | UserProfileController | Index |
| `/User/EditProfile` | `/UserProfile/Edit` | UserProfileController | Edit |
| `/User/UploadAvatar` | `/UserProfile/UploadAvatar` | UserProfileController | UploadAvatar |
| `/User/RemoveAvatar` | `/UserProfile/RemoveAvatar` | UserProfileController | RemoveAvatar |
| `/User/History` | `/UserProfile/History` | UserProfileController | History |
| `/User/WishlistSection` | `/UserProfile/WishlistSection` | UserProfileController | WishlistSection |
| `/User/ActiveOrdersSection` | `/UserProfile/ActiveOrdersSection` | UserProfileController | ActiveOrdersSection |
| `/User/OrderHistorySection` | `/UserProfile/OrderHistorySection` | UserProfileController | OrderHistorySection |
| `/User/Addresses` | `/UserAddress/Index` | UserAddressController | Index |
| `/User/AddAddress` | `/UserAddress/Create` | UserAddressController | Create |
| `/User/EditAddress/{id}` | `/UserAddress/Edit/{id}` | UserAddressController | Edit |
| `/User/DeleteAddress/{id}` | `/UserAddress/Delete/{id}` | UserAddressController | Delete |
| `/User/SetDefaultAddress/{id}` | `/UserAddress/SetDefault/{id}` | UserAddressController | SetDefault |
| `/User/MyRatings` | `/UserReview/MyRatings` | UserReviewController | MyRatings |
| `/User/LeaveReview` | `/UserReview/StoreReview` | UserReviewController | StoreReview |

### AdminController → Multiple Controllers

| **Original Route** | **New Route** | **Controller** | **Action** |
|-------------------|---------------|----------------|------------|
| `/Admin/Index` | `/AdminAnalytics/Index` | AdminAnalyticsController | Index |
| `/Admin/ManageSales` | `/AdminAnalytics/Sales` | AdminAnalyticsController | Sales |
| `/Admin/ListUsers` | `/AdminUser/Index` | AdminUserController | Index |
| `/Admin/AccountDetails/{id}` | `/AdminUser/Details/{id}` | AdminUserController | Details |
| `/Admin/RemoveRole` | `/AdminUser/RemoveRole` | AdminUserController | RemoveRole |
| `/Admin/ReindexFTS` | `/AdminSystem/ReindexSearch` | AdminSystemController | ReindexSearch |
| `/Admin/ReindexHealth` | `/AdminSystem/SearchHealth` | AdminSystemController | SearchHealth |
| `/Admin/CacheStats` | `/AdminSystem/CacheStats` | AdminSystemController | CacheStats |
| `/Admin/ClearFuzzyCache` | `/AdminSystem/ClearCache` | AdminSystemController | ClearCache |

## Backward Compatibility Routes

To maintain backward compatibility, the following routes should be added to Program.cs:

```csharp
// User Controller Backward Compatibility
app.MapControllerRoute(
    name: "user_legacy",
    pattern: "User/{action}",
    defaults: new { controller = "UserProfile" },
    constraints: new { action = "Index|EditProfile|UploadAvatar|RemoveAvatar|History|WishlistSection|ActiveOrdersSection|OrderHistorySection" });

app.MapControllerRoute(
    name: "user_address_legacy",
    pattern: "User/{action}",
    defaults: new { controller = "UserAddress" },
    constraints: new { action = "Addresses|AddAddress|EditAddress|DeleteAddress|SetDefaultAddress" });

app.MapControllerRoute(
    name: "user_review_legacy", 
    pattern: "User/{action}",
    defaults: new { controller = "UserReview" },
    constraints: new { action = "MyRatings|LeaveReview" });

// Admin Controller Backward Compatibility  
app.MapControllerRoute(
    name: "admin_analytics_legacy",
    pattern: "Admin/{action}",
    defaults: new { controller = "AdminAnalytics" },
    constraints: new { action = "Index|ManageSales" });

app.MapControllerRoute(
    name: "admin_user_legacy",
    pattern: "Admin/{action}",
    defaults: new { controller = "AdminUser" },
    constraints: new { action = "ListUsers|AccountDetails|RemoveRole" });

app.MapControllerRoute(
    name: "admin_system_legacy",
    pattern: "Admin/{action}",
    defaults: new { controller = "AdminSystem" },
    constraints: new { action = "ReindexFTS|ReindexHealth|CacheStats|ClearFuzzyCache" });
```

## Action Name Mapping

Some action names have been updated for consistency:

| **Old Action** | **New Action** | **Reason** |
|---------------|---------------|------------|
| `EditProfile` | `Edit` | Consistent with RESTful naming |
| `Addresses` | `Index` | Standard index action name |
| `AddAddress` | `Create` | RESTful naming convention |
| `LeaveReview` | `StoreReview` | More descriptive name |
| `ListUsers` | `Index` | Standard index action name |
| `AccountDetails` | `Details` | Consistent with RESTful naming |
| `ManageSales` | `Sales` | Simplified action name |
| `ReindexFTS` | `ReindexSearch` | More descriptive name |
| `ReindexHealth` | `SearchHealth` | Consistent naming pattern |
| `ClearFuzzyCache` | `ClearCache` | Simplified name |

## View File Updates Required

The following view files need to be updated to reference the new routes:

### Layout and Navigation
- `Views/Shared/_Layout.cshtml` - Update navigation links
- `Views/Shared/_AdminLayout.cshtml` - Update admin navigation  

### User Area Views
- Update all `@Html.ActionLink()` and `@Url.Action()` calls in user views
- Update form action attributes in address management views
- Update AJAX endpoints in dashboard sections

### Admin Area Views  
- Update all admin navigation and action links
- Update AJAX endpoints for system operations
- Update form action attributes in user management views

## Testing Checklist

- [ ] All original URLs redirect correctly to new controllers
- [ ] Navigation links work without 404 errors  
- [ ] AJAX endpoints function properly
- [ ] Form submissions redirect to correct controllers
- [ ] Breadcrumbs and back links work correctly
- [ ] Search and filter functionality preserved