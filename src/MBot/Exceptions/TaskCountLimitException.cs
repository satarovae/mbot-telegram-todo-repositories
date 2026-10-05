namespace MBot.Exceptions;

public sealed class TaskCountLimitException : Exception
{
    public TaskCountLimitException(int taskCountLimit)
        : base($"Превышено максимальное количество задач равное {taskCountLimit}")
    {
    }
}
