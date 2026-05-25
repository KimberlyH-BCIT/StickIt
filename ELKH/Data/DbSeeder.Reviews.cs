using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

// TABLE OF CONTENTS
// - Review seeding
// - Rating seeding

/// <summary>
/// Database seeding operations for store reviews and testimonials.
/// Handles creation of featured homepage reviews from verified buyers.
/// </summary>
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
        // â•‘ Idempotency Check - Skip if reviews already exist                  â•‘
        // ======================================================================
        // Only skip if our specific seed reviewer accounts already exist.
        // A blanket AnyAsync() would block re-seeding whenever a user has submitted any review.
        var seedEmails = new[] { "lovedeep@storereview.com", "evan@storereview.com", "kimberly@storereview.com" };
        if (await db.RegisteredUsers.AnyAsync(u => seedEmails.Contains(u.Email)))
        {
            return; // Seed reviews already exist
        }

        // ======================================================================
        // â•‘ Featured Store Review Data                                          â•‘
        // â•‘ Testimonials for homepage carousel display                         â•‘
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
        // â•‘ Create Reviewer Accounts and Store Reviews                         â•‘
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
