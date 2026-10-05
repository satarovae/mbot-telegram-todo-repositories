namespace MBot.Exceptions;

public sealed class DuplicateTaskException : Exception
{
    public DuplicateTaskException(string task)
        : base($"Задача '{task}' уже существует")
    {
    }
}
