namespace MBot.TelegramBot;

/// <summary>
/// Последовательно передаёт сообщения библиотеке бота и поддерживает отмену ожидания ввода.
/// </summary>
public sealed class ConsoleInputCoordinator : TextReader
{
    private readonly TextReader _input;
    private readonly CancellationToken _cancellationToken;
    private readonly SemaphoreSlim _nextMessage = new(1, 1);

    public ConsoleInputCoordinator(TextReader input, CancellationToken cancellationToken)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _cancellationToken = cancellationToken;
    }

    public override string? ReadLine()
    {
        try
        {
            _nextMessage.Wait(_cancellationToken);
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(_input.ReadLine).WaitAsync(_cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public void CompleteMessage()
    {
        _nextMessage.Release();
    }
}
