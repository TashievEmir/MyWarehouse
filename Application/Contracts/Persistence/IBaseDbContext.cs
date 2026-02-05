using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Persistence
{
    public interface IBaseDbContext
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
        int SaveChanges();
        EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;
        EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;
    }
}
