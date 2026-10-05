using MBot.Entities;
using MBot.DataAccess;

namespace MBot.Infrastructure.DataAccess;

/// <summary>
/// Потокобезопасное in-memory хранилище пользователей.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<ToDoUser> _users = new();
    private readonly object _syncRoot = new();

    public ToDoUser? GetUser(Guid userId)
    {
        lock (_syncRoot)
        {
            return _users.FirstOrDefault(user => user.UserId == userId);
        }
    }

    public ToDoUser? GetUserByTelegramUserId(long telegramUserId)
    {
        lock (_syncRoot)
        {
            return _users.FirstOrDefault(user => user.TelegramUserId == telegramUserId);
        }
    }

    public void Add(ToDoUser user)
    {
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
    }
}
