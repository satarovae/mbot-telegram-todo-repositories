using MBot.Entities;
namespace MBot.DataAccess;

/// <summary>
/// Хранилище пользователей бота.
/// </summary>
public interface IUserRepository
{
    ToDoUser? GetUser(Guid userId);

    ToDoUser? GetUserByTelegramUserId(long telegramUserId);

    void Add(ToDoUser user);
}
