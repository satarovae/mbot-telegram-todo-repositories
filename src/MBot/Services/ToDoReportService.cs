using MBot.DataAccess;

namespace MBot.Services;

/// <summary>
/// Сервис формирования статистики задач на основе репозитория.
/// </summary>
public sealed class ToDoReportService : IToDoReportService
{
    private readonly IToDoRepository _repository;

    public ToDoReportService(IToDoRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public (int total, int completed, int active, DateTime generatedAt) GetUserStats(Guid userId)
    {
        var items = _repository.GetAllByUserId(userId);
        var completed = items.Count(item => item.State == ToDoItemState.Completed);
        var active = items.Count(item => item.State == ToDoItemState.Active);
        return (items.Count, completed, active, DateTime.UtcNow);
    }
}
