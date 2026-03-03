using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories
{
    /// <summary>
    /// Base repository providing common CRUD operations for entities.
    /// Eliminates duplicate code across repositories while allowing custom methods.
    /// </summary>
    /// <typeparam name="TEntity">The entity type (e.g., UserProfileModel)</typeparam>
    /// <typeparam name="TKey">The primary key type (e.g., int, string)</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> where TEntity : class
    {
        protected readonly ApplicationDbContext Context;
        protected readonly ILogger Logger;

        protected RepositoryBase(ApplicationDbContext context, ILogger logger)
        {
            Context = context;
            Logger = logger;
        }

        /// <summary>
        /// Get an entity by its primary key.
        /// </summary>
        public virtual TEntity? GetById(TKey id)
        {
            try
            {
                return Context.Set<TEntity>().Find(id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                return null;
            }
        }

        /// <summary>
        /// Get an entity by its primary key asynchronously.
        /// </summary>
        public virtual async Task<TEntity?> GetByIdAsync(TKey id)
        {
            try
            {
                return await Context.Set<TEntity>().FindAsync(id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                return null;
            }
        }

        /// <summary>
        /// Get all entities of this type.
        /// </summary>
        public virtual IEnumerable<TEntity> GetAll()
        {
            try
            {
                return Context.Set<TEntity>().ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving all {EntityType}", typeof(TEntity).Name);
                return Enumerable.Empty<TEntity>();
            }
        }

        /// <summary>
        /// Get all entities of this type asynchronously.
        /// </summary>
        public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            try
            {
                return await Context.Set<TEntity>().ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving all {EntityType}", typeof(TEntity).Name);
                return Enumerable.Empty<TEntity>();
            }
        }

        /// <summary>
        /// Add a new entity to the database.
        /// </summary>
        public virtual void Add(TEntity entity)
        {
            Context.Set<TEntity>().Add(entity);
        }

        /// <summary>
        /// Add a new entity and save changes immediately.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual bool AddAndSave(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Add(entity);
                Context.SaveChanges();
                Logger.LogInformation("Added new {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }

        /// <summary>
        /// Add a new entity and save changes asynchronously.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual async Task<bool> AddAsync(TEntity entity)
        {
            try
            {
                await Context.Set<TEntity>().AddAsync(entity);
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

        /// <summary>
        /// Update an existing entity.
        /// </summary>
        public virtual void Update(TEntity entity)
        {
            Context.Set<TEntity>().Update(entity);
        }

        /// <summary>
        /// Update an existing entity and save changes immediately.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual bool UpdateAndSave(TEntity entity)
        {
            try
            {
                Context.Set<TEntity>().Update(entity);
                Context.SaveChanges();
                Logger.LogInformation("Updated {EntityType}", typeof(TEntity).Name);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating {EntityType}", typeof(TEntity).Name);
                return false;
            }
        }

        /// <summary>
        /// Update an existing entity and save changes asynchronously.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual async Task<bool> UpdateAsync(TEntity entity)
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

        /// <summary>
        /// Delete an entity from the database.
        /// </summary>
        public virtual void Delete(TEntity entity)
        {
            Context.Set<TEntity>().Remove(entity);
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

        /// <summary>
        /// Delete an entity by ID asynchronously.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public virtual async Task<bool> DeleteAsync(TKey id)
        {
            try
            {
                var entity = await GetByIdAsync(id);
                if (entity == null)
                {
                    Logger.LogWarning("Cannot delete {EntityType} with ID {Id} - not found", typeof(TEntity).Name, id);
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
        /// Save all pending changes to the database.
        /// </summary>
        public virtual void SaveChanges()
        {
            Context.SaveChanges();
        }

        /// <summary>
        /// Save all pending changes to the database asynchronously.
        /// </summary>
        public virtual async Task SaveChangesAsync()
        {
            await Context.SaveChangesAsync();
        }
    }
}
