namespace MBot.Exceptions;

/// <summary>
/// Сигнализирует о завершении текущего сеанса работы бота.
/// </summary>
public sealed class ExitSessionException : OperationCanceledException
{
    public ExitSessionException()
        : base("Сеанс работы бота завершён.")
    {
    }
}
