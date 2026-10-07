using MBot.Entities;

namespace MBot.Services;

/// <summary>
/// Сервис управления задачами пользователей.
/// </summary>
public interface IToDoService
{
    Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Возвращает задачи пользователя со статусом Active.
    /// </summary>
    Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix, CancellationToken ct);

    Task<ToDoItem> AddAsync(ToDoUser user, string name, CancellationToken ct);

    Task MarkCompletedAsync(Guid id, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
