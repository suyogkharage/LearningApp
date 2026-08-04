using LearningApp.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LearningApp.Api.Repositories;

public sealed class EfRepository<TEntity>(LearningAppDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = dbContext.Set<TEntity>();

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await _set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _set.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _set.FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _set.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public void Remove(TEntity entity) => _set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
