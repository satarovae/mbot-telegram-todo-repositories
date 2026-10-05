using System.Globalization;
using MBot.DataAccess;

namespace MBot;

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

    public IReadOnlyList<ToDoItem> GetAllByUserId(Guid userId)
    {
        return _repository.GetAllByUserId(userId);
    }

    public IReadOnlyList<ToDoItem> GetActiveByUserId(Guid userId)
    {
        return _repository.GetActiveByUserId(userId);
    }

    public IReadOnlyList<ToDoItem> Find(ToDoUser user, string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateString(namePrefix);
        var prefix = namePrefix.Trim();
        return _repository.Find(
            user.UserId,
            item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public ToDoItem Add(ToDoUser user, string name)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateString(name);

        var taskName = name.Trim();
        if (_repository.CountActive(user.UserId) >= _taskCountLimit)
        {
            throw new TaskCountLimitException(_taskCountLimit);
        }

        if (taskName.Length > _taskLengthLimit)
        {
            throw new TaskLengthLimitException(taskName.Length, _taskLengthLimit);
        }

        if (_repository.ExistsByName(user.UserId, taskName))
        {
            throw new DuplicateTaskException(taskName);
        }

        var item = new ToDoItem(user, taskName);
        _repository.Add(item);
        return item;
    }

    public void MarkCompleted(Guid id)
    {
        var item = FindById(id);

        if (item.State == ToDoItemState.Completed)
        {
            return;
        }

        item.State = ToDoItemState.Completed;
        item.StateChangedAt = DateTime.UtcNow;
        _repository.Update(item);
    }

    public void Delete(Guid id)
    {
        _ = FindById(id);
        _repository.Delete(id);
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

    private ToDoItem FindById(Guid id)
    {
        var item = _repository.Get(id);

        if (item is null)
        {
            throw new InvalidOperationException($"Задача с Id {id} не найдена.");
        }

        return item;
    }
}
