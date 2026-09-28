namespace MBot.Services;

/// <summary>
/// Формирует статистику задач пользователя.
/// </summary>
public interface IToDoReportService
{
    (int total, int completed, int active, DateTime generatedAt) GetUserStats(Guid userId);
}
