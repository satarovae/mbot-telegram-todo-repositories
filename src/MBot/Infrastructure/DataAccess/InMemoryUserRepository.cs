using MBot.DataAccess;
using MBot.Entities;

namespace MBot.Infrastructure.DataAccess;

/// <summary>
/// Потокобезопасное in-memory хранилище пользователей.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<ToDoUser> _users = new();
    private readonly object _syncRoot = new();

    public Task<ToDoUser?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_users.FirstOrDefault(user => user.UserId == userId));
        }
    }

    public Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_users.FirstOrDefault(user => user.TelegramUserId == telegramUserId));
        }
    }

    public Task AddAsync(ToDoUser user, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        lock (_syncRoot)
        {
            if (_users.Any(existing => existing.UserId == user.UserId))
            {
                throw new InvalidOperationException($"Пользователь с Id {user.UserId} уже существует.");
            }

            if (_users.Any(existing => existing.TelegramUserId == user.TelegramUserId))
            {
                throw new InvalidOperationException($"Пользователь Telegram с Id {user.TelegramUserId} уже существует.");
            }

            _users.Add(user);
        }

        return Task.CompletedTask;
    }
}
