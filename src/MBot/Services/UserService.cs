using MBot.Entities;
using MBot.DataAccess;

namespace MBot.Services;

/// <summary>
/// Хранит зарегистрированных пользователей в памяти приложения.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public ToDoUser RegisterUser(long telegramUserId, string telegramUserName)
    {
        if (string.IsNullOrWhiteSpace(telegramUserName))
        {
            throw new ArgumentException("Имя пользователя не должно быть пустым или состоять только из пробелов.", nameof(telegramUserName));
        }

        var existingUser = _userRepository.GetUserByTelegramUserId(telegramUserId);
        if (existingUser is not null)
        {
            return existingUser;
        }

        var user = new ToDoUser(telegramUserName.Trim())
        {
            TelegramUserId = telegramUserId
        };
        _userRepository.Add(user);
        return user;
    }

    public ToDoUser? GetUser(long telegramUserId)
    {
        return _userRepository.GetUserByTelegramUserId(telegramUserId);
    }
}
