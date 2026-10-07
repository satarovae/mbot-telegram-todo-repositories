using System.Globalization;
using MBot.DataAccess;
using MBot.Entities;
using MBot.Exceptions;

namespace MBot.Services;

/// <summary>
/// Выполняет операции с задачами и проверяет ограничения предметной области.
/// </summary>
public sealed class ToDoService : IToDoService
{
    public const int MinimumLimit = 1;
    public const int MaximumLimit = 100;

    private readonly IToDoRepository _repository;
    private readonly int _taskCountLimit;
    private readonly int _taskLengthLimit;
    private readonly SemaphoreSlim _addLock = new(1, 1);

    public ToDoService(IToDoRepository repository, int taskCountLimit, int taskLengthLimit)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _taskCountLimit = ParseAndValidateInt(
            taskCountLimit.ToString(CultureInfo.InvariantCulture),
            MinimumLimit,
            MaximumLimit);
        _taskLengthLimit = ParseAndValidateInt(
            taskLengthLimit.ToString(CultureInfo.InvariantCulture),
            MinimumLimit,
            MaximumLimit);
    }

    public int TaskCountLimit => _taskCountLimit;

    public int TaskLengthLimit => _taskLengthLimit;

    public Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return _repository.GetAllByUserIdAsync(userId, ct);
    }

    public Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return _repository.GetActiveByUserIdAsync(userId, ct);
    }

    public Task<IReadOnlyList<ToDoItem>> FindAsync(
        ToDoUser user,
        string namePrefix,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ValidateString(namePrefix);
        var prefix = namePrefix.Trim();
        return _repository.FindAsync(
            user.UserId,
            item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            ct);
    }

    public async Task<ToDoItem> AddAsync(ToDoUser user, string name, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ValidateString(name);
        var taskName = name.Trim();

        await _addLock.WaitAsync(ct);
        try
        {
            if (await _repository.CountActiveAsync(user.UserId, ct) >= _taskCountLimit)
            {
                throw new TaskCountLimitException(_taskCountLimit);
            }

            if (taskName.Length > _taskLengthLimit)
            {
                throw new TaskLengthLimitException(taskName.Length, _taskLengthLimit);
            }

            if (await _repository.ExistsByNameAsync(user.UserId, taskName, ct))
            {
                throw new DuplicateTaskException(taskName);
            }

            var item = new ToDoItem(user, taskName);
            await _repository.AddAsync(item, ct);
            return item;
        }
        finally
        {
            _addLock.Release();
        }
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct)
    {
        var item = await FindByIdAsync(id, ct);
        if (item.State == ToDoItemState.Completed)
        {
            return;
        }

        item.State = ToDoItemState.Completed;
        item.StateChangedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(item, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _ = await FindByIdAsync(id, ct);
        await _repository.DeleteAsync(id, ct);
    }

    public int ParseAndValidateInt(string? str, int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentException("Минимальное значение не может быть больше максимального.");
        }

        if (!int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Введите целое число от {min} до {max}.");
        }

        if (value < min || value > max)
        {
            throw new ArgumentException($"Число должно находиться в диапазоне от {min} до {max}.");
        }

        return value;
    }

    public void ValidateString(string? str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentException("Строка не должна быть пустой или состоять только из пробелов.");
        }
    }

    private async Task<ToDoItem> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _repository.GetAsync(id, ct);
        return item ?? throw new InvalidOperationException($"Задача с Id {id} не найдена.");
    }
}
