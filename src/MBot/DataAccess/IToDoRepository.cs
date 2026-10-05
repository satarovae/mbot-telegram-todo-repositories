using MBot.Entities;
namespace MBot.DataAccess;

/// <summary>
/// Хранилище задач пользователей.
/// </summary>
public interface IToDoRepository
{
    IReadOnlyList<ToDoItem> GetAllByUserId(Guid userId);

    IReadOnlyList<ToDoItem> GetActiveByUserId(Guid userId);

    ToDoItem? Get(Guid id);

    void Add(ToDoItem item);

    void Update(ToDoItem item);

    void Delete(Guid id);

    bool ExistsByName(Guid userId, string name);

    int CountActive(Guid userId);

    IReadOnlyList<ToDoItem> Find(Guid userId, Func<ToDoItem, bool> predicate);
}
