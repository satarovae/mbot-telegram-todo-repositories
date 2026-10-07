using MBot.DataAccess;
using MBot.Entities;

namespace MBot.Infrastructure.DataAccess;

/// <summary>
/// Потокобезопасное in-memory хранилище задач.
/// </summary>
public sealed class InMemoryToDoRepository : IToDoRepository
{
    private readonly List<ToDoItem> _items = new();
    private readonly object _syncRoot = new();

    public Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<ToDoItem>>(
                _items.Where(item => item.User.UserId == userId).ToList().AsReadOnly());
        }
    }

    public Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<ToDoItem>>(_items
                .Where(item => item.User.UserId == userId && item.State == ToDoItemState.Active)
                .ToList()
                .AsReadOnly());
        }
    }

    public Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }
    }

    public Task AddAsync(ToDoItem item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(item);
        lock (_syncRoot)
        {
            if (_items.Any(existing => existing.Id == item.Id))
            {
                throw new InvalidOperationException($"Задача с Id {item.Id} уже существует.");
            }

            _items.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(ToDoItem item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
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

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var removed = _items.RemoveAll(item => item.Id == id);
            if (removed == 0)
            {
                throw new InvalidOperationException($"Задача с Id {id} не найдена.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_syncRoot)
        {
            return Task.FromResult(_items.Any(item =>
                item.User.UserId == userId &&
                string.Equals(item.Name, name, StringComparison.Ordinal)));
        }
    }

    public Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_items.Count(item =>
                item.User.UserId == userId && item.State == ToDoItemState.Active));
        }
    }

    public Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<ToDoItem>>(_items
                .Where(item => item.User.UserId == userId)
                .Where(predicate)
                .ToList()
                .AsReadOnly());
        }
    }
}
