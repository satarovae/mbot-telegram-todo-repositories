namespace MBot.Services;

/// <summary>
/// Формирует статистику задач пользователя.
/// </summary>
public interface IToDoReportService
{
    Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStatsAsync(Guid userId, CancellationToken ct);
}
