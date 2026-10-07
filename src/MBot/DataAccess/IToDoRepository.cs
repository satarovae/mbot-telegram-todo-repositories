using MBot.Entities;

namespace MBot.DataAccess;

/// <summary>
/// Хранилище задач пользователей.
/// </summary>
public interface IToDoRepository
{
    Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

    Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct);

    Task AddAsync(ToDoItem item, CancellationToken ct);

    Task UpdateAsync(ToDoItem item, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct);

    Task<int> CountActiveAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct);
}
