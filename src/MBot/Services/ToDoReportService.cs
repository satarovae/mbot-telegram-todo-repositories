using MBot.DataAccess;
using MBot.Entities;

namespace MBot.Services;

/// <summary>
/// Формирует статистику задач на основе репозитория.
/// </summary>
public sealed class ToDoReportService : IToDoReportService
{
    private readonly IToDoRepository _repository;

    public ToDoReportService(IToDoRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStatsAsync(
        Guid userId,
        CancellationToken ct)
    {
        var items = await _repository.GetAllByUserIdAsync(userId, ct);
        var completed = items.Count(item => item.State == ToDoItemState.Completed);
        var active = items.Count(item => item.State == ToDoItemState.Active);
        return (items.Count, completed, active, DateTime.UtcNow);
    }
}
