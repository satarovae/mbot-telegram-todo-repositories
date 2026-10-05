namespace MBot;

/// <summary>
/// Сервис управления задачами пользователей.
/// </summary>
public interface IToDoService
{
    IReadOnlyList<ToDoItem> GetAllByUserId(Guid userId);

    /// <summary>
    /// Возвращает задачи пользователя со статусом Active.
    /// </summary>
    IReadOnlyList<ToDoItem> GetActiveByUserId(Guid userId);

    IReadOnlyList<ToDoItem> Find(ToDoUser user, string namePrefix);

    ToDoItem Add(ToDoUser user, string name);

    void MarkCompleted(Guid id);

    void Delete(Guid id);
}
