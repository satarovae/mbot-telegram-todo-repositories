using MBot.DataAccess;
using MBot.Entities;

namespace MBot.Services;

/// <summary>
/// Регистрирует пользователей и предоставляет данные о регистрации.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<ToDoUser> RegisterUserAsync(
        long telegramUserId,
        string telegramUserName,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(telegramUserName))
        {
            throw new ArgumentException(
                "Имя пользователя не должно быть пустым или состоять только из пробелов.",
                nameof(telegramUserName));
        }

        await _registrationLock.WaitAsync(ct);
        try
        {
            var existingUser = await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
            if (existingUser is not null)
            {
                return existingUser;
            }

            var user = new ToDoUser(telegramUserName.Trim())
            {
                TelegramUserId = telegramUserId
            };

            await _userRepository.AddAsync(user, ct);
            return user;
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    public Task<ToDoUser?> GetUserAsync(long telegramUserId, CancellationToken ct)
    {
        return _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
    }
}
