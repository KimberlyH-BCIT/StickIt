using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories
{
    /// <summary>
    /// Base repository providing common CRUD operations for entities.
    /// All write operations are async-only to avoid blocking thread-pool threads.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> where TEntity : class
    {
        protected readonly ApplicationDbContext Context;
        protected readonly ILogger Logger;

        protected RepositoryBase(ApplicationDbContext context, ILogger logger)
        {
            Context = context;
            Logger = logger;
        }

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>Get an entity by its primary key synchronously (prefer the async overload).</summary>
        public virtual TEntity? GetById(TKey id)
        {
            try   { return Context.Set<TEntity>().Find(id); }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                throw;
            }
        }

        /// <summary>Get an entity by its primary key asynchronously.</summary>
        public virtual async Task<TEntity?> GetByIdAsync(TKey id)
        {
            try   { return await Context.Set<TEntity>().FindAsync(id); }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                throw;
            }
        }

        /// <summary>Get all entities (prefer the async overload).</summary>
        public virtual IEnumerable<TEntity> GetAll()
        {
            try   { return Context.Set<TEntity>().ToList(); }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving all {EntityType}", typeof(TEntity).Name);
                throw;
            }
        }

        /// <summary>Get all entities asynchronously.</summary>
        public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            try   { return await Context.Set<TEntity>().ToListAsync(); }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving all {EntityType}", typeof(TEntity).Name);
                throw;
            }
        }

        // ── Write (staging only — no immediate save) ──────────────────────────

        /// <summary>Stage a new entity for insertion (does not save).</summary>
        public virtual void Add(TEntity entity) => Context.Set<TEntity>().Add(entity);

        /// <summary>Stage an updated entity (does not save).</summary>
        public virtual void Update(TEntity entity) => Context.Set<TEntity>().Update(entity);

        /// <summary>Stage an entity for deletion (does not save).</summary>
        public virtual void Delete(TEntity entity) => Context.Set<TEntity>().Remove(entity);

        // ── Write + Save (async) ──────────────────────────────────────────────

        /// <summary>
        /// Add and immediately persist an entity.
        /// Uses <c>Add</c> (not <c>AddAsync</c>) — the async overload is only beneficial
        /// for HiLo key generation, not the default auto-increment strategy.
        /// </summary>
        public virtual async Task<bool> AddAndSaveAsync(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Add(entity);
                await Context.SaveChangesAsync();
                Logger.LogInformation("Added new {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }


        /// <summary>Update and immediately persist an entity.</summary>
        public virtual async Task<bool> UpdateAndSaveAsync(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Update(entity);
                await Context.SaveChangesAsync();
                Logger.LogInformation("Updated {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }

        /// <summary>Delete an entity by ID and immediately persist.</summary>
        public virtual async Task<bool> DeleteAsync(TKey id)
        {
            try
            {
                var entity = await GetByIdAsync(id);
                if (entity is null)
                {
                    Logger.LogWarning("Cannot delete {EntityType} with ID {Id} — not found", typeof(TEntity).Name, id);
                    return false;
                }

                Context.Set<TEntity>().Remove(entity);
                await Context.SaveChangesAsync();

                Logger.LogInformation("Deleted {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                return false;
            }
        }

        /// <summary>
        /// Delete an entity and save changes immediately.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual bool DeleteAndSave(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Remove(entity);
                Context.SaveChanges();

                Logger.LogInformation("Deleted {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }

        /// <summary>Delete an entity instance and immediately persist.</summary>
        public virtual async Task<bool> DeleteAndSaveAsync(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Remove(entity);
                await Context.SaveChangesAsync();
                Logger.LogInformation("Deleted {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }

        /// <summary>Flush all pending changes asynchronously.</summary>
        public virtual async Task SaveChangesAsync() => await Context.SaveChangesAsync();
    }
}
