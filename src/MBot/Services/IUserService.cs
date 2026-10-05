using MBot.Entities;
namespace MBot.Services;

/// <summary>
/// Сервис регистрации и поиска пользователей.
/// </summary>
public interface IUserService
{
    ToDoUser RegisterUser(long telegramUserId, string telegramUserName);

    ToDoUser? GetUser(long telegramUserId);
}
