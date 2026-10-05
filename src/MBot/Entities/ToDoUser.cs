namespace MBot.Entities;

/// <summary>
/// Пользователь менеджера задач.
/// </summary>
public sealed class ToDoUser
{
    public ToDoUser(string telegramUserName)
    {
        if (string.IsNullOrWhiteSpace(telegramUserName))
        {
            throw new ArgumentException("Имя пользователя не должно быть пустым или состоять только из пробелов.", nameof(telegramUserName));
        }

        UserId = Guid.NewGuid();
        TelegramUserName = telegramUserName.Trim();
        RegisteredAt = DateTime.UtcNow;
    }

    public Guid UserId { get; set; }

    public long TelegramUserId { get; set; }

    public string TelegramUserName { get; set; }

    public DateTime RegisteredAt { get; set; }
}
