# Controller Decomposition Route Mapping

## Status

This file is an archived planning artifact from an earlier controller-decomposition effort. It is kept as historical reference only.

The current application does not implement the legacy compatibility routes that were once proposed here. Live routing is defined in `ELKH/Extensions/ApplicationBuilderExtensions.cs` through the area route, the default controller route, and Razor Pages endpoint mapping.

## Historical Notes

The tables below show the intended route/action split that was being considered when larger `UserController` and `AdminController` responsibilities were being decomposed. Treat them as migration notes, not as current behavior or implementation instructions.

### UserController → Multiple Controllers

| Original Route | Planned Route | Planned Controller | Planned Action |
|---|---|---|---|
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

| Original Route | Planned Route | Planned Controller | Planned Action |
|---|---|---|---|
| `/Admin/Index` | `/AdminAnalytics/Index` | AdminAnalyticsController | Index |
| `/Admin/ManageSales` | `/AdminAnalytics/Sales` | AdminAnalyticsController | Sales |
| `/Admin/ListUsers` | `/AdminUser/Index` | AdminUserController | Index |
| `/Admin/AccountDetails/{id}` | `/AdminUser/Details/{id}` | AdminUserController | Details |
| `/Admin/RemoveRole` | `/AdminUser/RemoveRole` | AdminUserController | RemoveRole |
| `/Admin/ReindexFTS` | `/AdminSystem/ReindexSearch` | AdminSystemController | ReindexSearch |
| `/Admin/ReindexHealth` | `/AdminSystem/SearchHealth` | AdminSystemController | SearchHealth |
| `/Admin/CacheStats` | `/AdminSystem/CacheStats` | AdminSystemController | CacheStats |
| `/Admin/ClearFuzzyCache` | `/AdminSystem/ClearCache` | AdminSystemController | ClearCache |

## Current Guidance

- Do not use this file as a checklist for `Program.cs` or endpoint changes.
- If route compatibility work is revived later, validate the live routes first and create a new implementation plan against the current codebase.
- Keep portfolio-facing docs tied to implemented routes rather than archived plans.
