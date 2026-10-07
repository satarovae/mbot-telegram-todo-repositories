using MBot.Entities;

namespace MBot.DataAccess;

/// <summary>
/// Хранилище пользователей бота.
/// </summary>
public interface IUserRepository
{
    Task<ToDoUser?> GetUserAsync(Guid userId, CancellationToken ct);

    Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct);

    Task AddAsync(ToDoUser user, CancellationToken ct);
}
