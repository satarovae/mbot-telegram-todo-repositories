using MBot.Entities;
using MBot.DataAccess;

namespace MBot.Infrastructure.DataAccess;

/// <summary>
/// Потокобезопасное in-memory хранилище задач.
/// </summary>
public sealed class InMemoryToDoRepository : IToDoRepository
{
    private readonly List<ToDoItem> _items = new();
    private readonly object _syncRoot = new();

    public IReadOnlyList<ToDoItem> GetAllByUserId(Guid userId)
    {
        lock (_syncRoot)
        {
            return _items.Where(item => item.User.UserId == userId).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ToDoItem> GetActiveByUserId(Guid userId)
    {
        lock (_syncRoot)
        {
            return _items
                .Where(item => item.User.UserId == userId && item.State == ToDoItemState.Active)
                .ToList()
                .AsReadOnly();
        }
    }

    public ToDoItem? Get(Guid id)
    {
        lock (_syncRoot)
        {
            return _items.FirstOrDefault(item => item.Id == id);
        }
    }

    public void Add(ToDoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            if (_items.Any(existing => existing.Id == item.Id))
            {
                throw new InvalidOperationException($"Задача с Id {item.Id} уже существует.");
            }

            _items.Add(item);
        }
    }

    public void Update(ToDoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            var index = _items.FindIndex(existing => existing.Id == item.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Задача с Id {item.Id} не найдена.");
            }

            _items[index] = item;
        }
    }

    public void Delete(Guid id)
    {
        lock (_syncRoot)
        {
            var removed = _items.RemoveAll(item => item.Id == id);
            if (removed == 0)
            {
                throw new InvalidOperationException($"Задача с Id {id} не найдена.");
            }
        }
    }

    public bool ExistsByName(Guid userId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            return _items.Any(item =>
                item.User.UserId == userId &&
                string.Equals(item.Name, name, StringComparison.Ordinal));
        }
    }

    public int CountActive(Guid userId)
    {
        lock (_syncRoot)
        {
            return _items.Count(item => item.User.UserId == userId && item.State == ToDoItemState.Active);
        }
    }

    public IReadOnlyList<ToDoItem> Find(Guid userId, Func<ToDoItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_syncRoot)
        {
            return _items
                .Where(item => item.User.UserId == userId)
                .Where(predicate)
                .ToList()
                .AsReadOnly();
        }
    }
}
