namespace MBot.Entities;

/// <summary>
/// Задача пользователя.
/// </summary>
public sealed class ToDoItem
{
    public ToDoItem(ToDoUser user, string name)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Название задачи не должно быть пустым или состоять только из пробелов.", nameof(name));
        }

        Id = Guid.NewGuid();
        User = user;
        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
        State = ToDoItemState.Active;
        StateChangedAt = null;
    }

    public Guid Id { get; set; }

    public ToDoUser User { get; set; }

    public string Name { get; set; }

    public DateTime CreatedAt { get; set; }

    public ToDoItemState State { get; set; }

    public DateTime? StateChangedAt { get; set; }
}
