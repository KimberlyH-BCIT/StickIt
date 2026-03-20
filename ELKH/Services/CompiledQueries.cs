using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Contains compiled EF Core queries for frequently-executed hot paths.
    /// Compiled queries are pre-compiled and cached, eliminating the overhead
    /// of query translation on each execution. Use for queries that run thousands
    /// of times with only parameter changes.
    /// </summary>
    public static class CompiledQueries
    {
        /// <summary>
        /// Compiled query to retrieve a user by email.
        /// Approximately 20-30% faster than standard FirstOrDefaultAsync after warmup.
        /// The CancellationToken in the Func signature is threaded through by EF Core automatically.
        /// </summary>
        public static readonly Func<ApplicationDbContext, string, CancellationToken, Task<RegisteredUserModel?>>
            GetUserByEmail = EF.CompileAsyncQuery(
                (ApplicationDbContext db, string email, CancellationToken ct) =>
                    db.RegisteredUsers
                        .AsNoTracking()
                        .FirstOrDefault(u => u.Email == email));

        /// <summary>
        /// Compiled query to retrieve a wishlist by user ID.
        /// </summary>
        public static readonly Func<ApplicationDbContext, int, Task<WishListModel?>>
            GetWishlistByUserId = EF.CompileAsyncQuery(
                (ApplicationDbContext db, int userId) =>
                    db.WishLists
                        .AsNoTracking()
                        .FirstOrDefault(w => w.FkUserId == userId));

        /// <summary>
        /// Compiled query to count cart items for a user.
        /// </summary>
        public static readonly Func<ApplicationDbContext, int, Task<int>>
            CountCartItems = EF.CompileAsyncQuery(
                (ApplicationDbContext db, int userId) =>
                    db.Carts.Count(c => c.FkRegisteredUserId == userId));

        /// <summary>
        /// Compiled query to retrieve a product by ID with category.
        /// The CancellationToken in the Func signature is threaded through by EF Core automatically.
        /// </summary>
        public static readonly Func<ApplicationDbContext, int, CancellationToken, Task<ProductModel?>>
            GetProductById = EF.CompileAsyncQuery(
                (ApplicationDbContext db, int productId, CancellationToken ct) =>
                    db.Product
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .FirstOrDefault(p => p.PkProductId == productId));
    }
}
