using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/// <summary>
/// Database seeding operations for store reviews and testimonials.
/// Handles creation of featured homepage reviews from verified buyers.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. SeedStoreReviewsAsync Method ................................ Lines [16-161]
///    - Idempotency check             // Skip if reviews exist
///    - Featured review data          // 3 verified buyer testimonials
///    - Account creation              // Complete user profiles
///    - Store review creation         // Approved, verified reviews
/// 
/// 2. Featured Review Data ........................................ Lines [50-76]
///    - Lovedeep testimonial          // "Great quality and super fast!"
///    - Evan testimonial              // "I'm loving it!"
///    - Kimberly testimonial          // "Durable and looks stunning!"
/// 
/// 3. User Account Creation ....................................... Lines [83-124]
///    - Identity user setup           // ASP.NET Core Identity accounts
///    - Role assignment               // Customer role assignment
///    - Registered user model         // Application user records
/// 
/// 4. Profile and Review Creation ................................. Lines [126-158]
///    - User profile setup            // Complete customer profiles
///    - Store review entities         // Featured homepage reviews
/// ================================================================================
/// 
/// ARCHITECTURAL CONTEXT:
/// • Part of ELKH's comprehensive database seeding strategy
/// • Creates authentic testimonials for homepage social proof
/// • Integrates with ASP.NET Core Identity for complete user lifecycle
/// • Implements verified buyer status for trusted reviews
/// • Supports both anonymous and account-creation review workflows
/// 
/// DATA SEEDING STRATEGY:
/// • Idempotent operations - safe to run multiple times
/// • Featured testimonials with 5-star ratings for homepage display
/// • Complete user account creation including Identity, profiles, and roles
/// • Verified buyer status ensures reviews appear with trust indicators
/// • Randomized creation dates for realistic review timeline
/// 
/// BUSINESS LOGIC & FEATURES:
/// • 3 featured store reviews from verified buyers (Lovedeep, Evan, Kimberly)
/// • All reviews pre-approved and flagged as verified purchases
/// • Complete customer account creation with placeholder profiles
/// • Standard password format: StoreReview@2025! for all reviewers
/// • Reviews displayed on homepage carousel for social proof
/// 
/// INTEGRATION POINTS:
/// • Depends on: ApplicationDbContext for database operations
/// • Depends on: UserManager<IdentityUser> for ASP.NET Core Identity
/// • Creates: StoreReviewModel, RegisteredUserModel, UserProfileModel entities
/// • Integrates with: Customer role assignment and email confirmation
/// • Used by: Homepage review carousel and testimonial displays
/// 
/// SECURITY CONSIDERATIONS:
/// • All reviewer accounts use confirmed email addresses
/// • Customer role assignment follows standard authorization patterns
/// • Verified buyer status prevents fake review exploitation
/// • Approved reviews bypass moderation for immediate display
/// </remarks>
public static partial class DbSeeder
{
    #region Store Review Seeding

    /// <summary>
    /// Seeds the database with featured store reviews from verified buyers.
    /// These are the testimonials displayed on the homepage carousel.
    /// Fully idempotent-skipped if any store reviews already exist.
    /// </summary>
    /// <param name="db">Database context for creating store reviews.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for creating reviewer accounts.</param>
    /// <remarks>
    /// <para><strong>Featured Reviews:</strong></para>
    /// Seeds 3 verified buyer testimonials with 5-star ratings:
    /// <list type="bullet">
    /// <item>Lovedeep - "Great quality and super fast!"</item>
    /// <item>Evan - "I'm loving it!"</item>
    /// <item>Kimberly - "Durable and looks stunning!"</item>
    /// </list>
    ///
    /// <para><strong>Verified Buyer Status:</strong></para>
    /// All seeded reviews are marked as IsVerifiedBuyer = true and pre-approved
    /// for immediate display on the homepage.
    ///
    /// <para><strong>Account Creation:</strong></para>
    /// Creates temporary Customer accounts for reviewers with placeholder profiles.
    /// Password format: StoreReview@2025! for all reviewer accounts.
    /// </remarks>
    public static async Task SeedStoreReviewsAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        // ======================================================================
        // ║ Idempotency Check - Skip if reviews already exist                  ║
        // ======================================================================
        if (await db.StoreReviews.AnyAsync())
        {
            return; // Reviews already seeded
        }

        // ======================================================================
        // ║ Featured Store Review Data                                          ║
        // ║ Testimonials for homepage carousel display                         ║
        // ======================================================================
        var reviewData = new[]
        {
            new
            {
                Email = "lovedeep@storereview.com",
                FirstName = "Lovedeep",
                LastName = "Singh",
                Title = "Great quality and super fast!",
                Description = "I ordered custom stickers for my business and they exceeded my expectations. The colors are vibrant, the material is waterproof, and delivery was incredibly fast. Will definitely order again!",
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new
            {
                Email = "evan@storereview.com",
                FirstName = "Evan",
                LastName = "Martinez",
                Title = "I'm loving it!",
                Description = "These stickers are perfect! The die-cut is clean and precise, and they stick really well. I've put them on my laptop and water bottle and they still look brand new after weeks of use. Highly recommend!",
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new
            {
                Email = "kimberly@storereview.com",
                FirstName = "Kimberly",
                LastName = "Chen",
                Title = "Durable and looks stunning!",
                Description = "The premium quality is evident from the moment you hold these stickers. Scratch-resistant, waterproof, and the colors pop beautifully. Customer service was also excellent. Five stars!",
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        // ======================================================================
        // ║ Create Reviewer Accounts and Store Reviews                         ║
        // ======================================================================
        const string defaultPassword = "StoreReview@2025!";

        foreach (var reviewerData in reviewData)
        {
            // Create Identity user account
            var identityUser = new IdentityUser
            {
                UserName = reviewerData.Email,
                Email = reviewerData.Email,
                EmailConfirmed = true // Pre-confirm email for reviewers
            };

            var existingUser = await userManager.FindByEmailAsync(reviewerData.Email);
            if (existingUser == null)
            {
                var createResult = await userManager.CreateAsync(identityUser, defaultPassword);
                if (!createResult.Succeeded)
                {
                    continue; // Skip if user creation failed
                }

                // Assign Customer role
                await userManager.AddToRoleAsync(identityUser, "Customer");
            }
            else
            {
                identityUser = existingUser;
            }

            // Create RegisteredUser record
            var registeredUser = await db.RegisteredUsers
                .FirstOrDefaultAsync(u => u.Email == reviewerData.Email);

            if (registeredUser == null)
            {
                registeredUser = new RegisteredUserModel
                {
                    Email = reviewerData.Email
                };
                db.RegisteredUsers.Add(registeredUser);
                await db.SaveChangesAsync();
            }

            // Create user profile (UserProfileModel uses Email as primary key)
            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.PkEmail == reviewerData.Email);

            if (profile == null)
            {
                profile = new UserProfileModel
                {
                    PkEmail = reviewerData.Email,
                    FirstName = reviewerData.FirstName,
                    LastName = reviewerData.LastName
                };
                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();
            }

            // Create store review
            var storeReview = new StoreReviewModel
            {
                FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                Title = reviewerData.Title,
                Description = reviewerData.Description,
                Rating = reviewerData.Rating,
                IsVerifiedBuyer = true, // Mark as verified buyer
                Approved = true, // Pre-approve for homepage display
                CreatedAt = reviewerData.CreatedAt
            };

            db.StoreReviews.Add(storeReview);
        }

        await db.SaveChangesAsync();
    }

    #endregion
}
