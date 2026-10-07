using System.Diagnostics;
using System.Globalization;
using System.Text;
using MBot.Entities;
using MBot.Exceptions;
using MBot.Services;
using Otus.ToDoList.ConsoleBot;
using Otus.ToDoList.ConsoleBot.Types;

namespace MBot.TelegramBot;

/// <summary>
/// Обрабатывает входящие сообщения и формирует ответы бота.
/// </summary>
public sealed class UpdateHandler : IUpdateHandler
{
    private readonly IUserService _userService;
    private readonly IToDoService _toDoService;
    private readonly IToDoReportService _reportService;
    private readonly Action? _stopReceiving;
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private bool _exited;

    public event Action? UpdateProcessed;

    public UpdateHandler(
        IUserService userService,
        IToDoService toDoService,
        IToDoReportService reportService,
        Action? stopReceiving = null)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _toDoService = toDoService ?? throw new ArgumentNullException(nameof(toDoService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _stopReceiving = stopReceiving;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(botClient);

        await _commandLock.WaitAsync(ct);
        try
        {
            if (_exited)
            {
                return;
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(update);
                ArgumentNullException.ThrowIfNull(update.Message);
                ArgumentNullException.ThrowIfNull(update.Message.Chat);
                ArgumentNullException.ThrowIfNull(update.Message.From);

                var messageText = update.Message.Text?.Trim();
                if (string.IsNullOrWhiteSpace(messageText))
                {
                    throw new ArgumentException("Команда не введена. Введите /help для просмотра справки.");
                }

                var (command, argument) = ParseCommand(messageText);
                switch (command)
                {
                    case "/start":
                        await HandleStartAsync(botClient, update, ct);
                        return;
                    case "/help":
                        await HandleHelpAsync(botClient, update, ct);
                        return;
                    case "/info":
                        await HandleInfoAsync(botClient, update, ct);
                        return;
                    case "/exit":
                        await HandleExitAsync(botClient, update, ct);
                        return;
                }

                var user = await _userService.GetUserAsync(update.Message.From.Id, ct);
                if (user is null)
                {
                    await botClient.SendMessage(
                        update.Message.Chat,
                        "Пользователь не зарегистрирован. Выполните /start. До регистрации доступны команды /help, /info и /exit.",
                        ct);
                    return;
                }

                switch (command)
                {
                    case "/addtask":
                        await HandleAddTaskAsync(botClient, update, user, argument, ct);
                        break;
                    case "/showtasks":
                        await HandleShowTasksAsync(botClient, update, user, ct);
                        break;
                    case "/showalltasks":
                        await HandleShowAllTasksAsync(botClient, update, user, ct);
                        break;
                    case "/completetask":
                        await HandleCompleteTaskAsync(botClient, update, user, argument, ct);
                        break;
                    case "/removetask":
                        await HandleRemoveTaskAsync(botClient, update, user, argument, ct);
                        break;
                    case "/report":
                        await HandleReportAsync(botClient, update, user, ct);
                        break;
                    case "/find":
                        await HandleFindAsync(botClient, update, user, argument, ct);
                        break;
                    default:
                        await botClient.SendMessage(
                            update.Message.Chat,
                            $"Неизвестная команда \"{command}\". Введите /help для просмотра справки.",
                            ct);
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (update?.Message?.Chat is { } chat)
                {
                    await botClient.SendMessage(chat, BuildExceptionMessage(exception), ct);
                }
                else
                {
                    await HandleErrorAsync(botClient, exception, ct);
                }
            }
        }
        finally
        {
            _commandLock.Release();
            UpdateProcessed?.Invoke();
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        Console.Error.WriteLine($"HandleError: {exception}");
        return Task.CompletedTask;
    }

    private async Task HandleStartAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        var telegramUserId = update.Message.From.Id;
        var existingUser = await _userService.GetUserAsync(telegramUserId, ct);
        if (existingUser is not null)
        {
            await botClient.SendMessage(
                update.Message.Chat,
                $"Пользователь {existingUser.TelegramUserName} уже зарегистрирован. Введите /help для просмотра команд.",
                ct);
            return;
        }

        var telegramUserName = string.IsNullOrWhiteSpace(update.Message.From.Username)
            ? $"user_{telegramUserId.ToString(CultureInfo.InvariantCulture)}"
            : update.Message.From.Username!;

        var user = await _userService.RegisterUserAsync(telegramUserId, telegramUserName, ct);
        await botClient.SendMessage(
            update.Message.Chat,
            $"Добрый день, {user.TelegramUserName}. Регистрация выполнена. Введите /help для просмотра команд.",
            ct);
    }

    private async Task HandleExitAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        _sessionTimer.Stop();
        await botClient.SendMessage(
            update.Message.Chat,
            $"До свидания! Время текущего сеанса: {FormatElapsed(_sessionTimer.Elapsed)}.",
            ct);
        _exited = true;
        _stopReceiving?.Invoke();
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours} ч {elapsed.Minutes} мин {elapsed.Seconds} с";
        }

        return elapsed.TotalMinutes >= 1
            ? $"{elapsed.Minutes} мин {elapsed.Seconds} с"
            : $"{Math.Max(1, (int)Math.Ceiling(elapsed.TotalSeconds))} с";
    }

    private async Task HandleHelpAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        var isRegistered = await _userService.GetUserAsync(update.Message.From.Id, ct) is not null;
        var builder = new StringBuilder();
        builder.AppendLine("Доступные команды:");
        builder.AppendLine("/start - зарегистрироваться в боте;");
        builder.AppendLine("/help - показать справку;");
        builder.AppendLine("/info - показать информацию о боте;");
        builder.AppendLine("/exit - завершить работу бота;");

        if (isRegistered)
        {
            builder.AppendLine("/addtask <название задачи> - добавить задачу;");
            builder.AppendLine("/showtasks - показать активные задачи;");
            builder.AppendLine("/showalltasks - показать все задачи;");
            builder.AppendLine("/completetask <Id> - отметить задачу выполненной;");
            builder.AppendLine("/removetask <Id> - удалить задачу;");
            builder.AppendLine("/report - показать статистику задач;");
            builder.Append("/find <начало названия> - найти задачи по началу названия.");
        }
        else
        {
            builder.Append("Для работы с задачами сначала выполните /start.");
        }

        await botClient.SendMessage(update.Message.Chat, builder.ToString(), ct);
    }

    private static Task HandleInfoAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        return botClient.SendMessage(
            update.Message.Chat,
            "Бот для управления списком задач. Команды обрабатываются через IUpdateHandler, данные пользователей и задач - через сервисные интерфейсы.",
            ct);
    }

    private async Task HandleAddTaskAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Укажите название задачи. Пример: /addtask Новая задача");
        }

        var task = await _toDoService.AddAsync(user, argument, ct);
        await botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" добавлена. Id: {task.Id}.", ct);
    }

    private async Task HandleShowTasksAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        CancellationToken ct)
    {
        var tasks = await _toDoService.GetActiveByUserIdAsync(user.UserId, ct);
        var text = tasks.Count == 0
            ? "Список активных задач пуст."
            : FormatTasks("Активные задачи:", tasks, includeState: false);
        await botClient.SendMessage(update.Message.Chat, text, ct);
    }

    private async Task HandleShowAllTasksAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        CancellationToken ct)
    {
        var tasks = await _toDoService.GetAllByUserIdAsync(user.UserId, ct);
        var text = tasks.Count == 0
            ? "Список задач пуст."
            : FormatTasks("Все задачи:", tasks, includeState: true);
        await botClient.SendMessage(update.Message.Chat, text, ct);
    }

    private async Task HandleCompleteTaskAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument,
        CancellationToken ct)
    {
        var taskId = ParseTaskId(argument, "/completetask");
        var task = await FindUserTaskAsync(user.UserId, taskId, ct);
        if (task.State == ToDoItemState.Completed)
        {
            await botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" уже выполнена.", ct);
            return;
        }

        await _toDoService.MarkCompletedAsync(taskId, ct);
        await botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" отмечена как выполненная.", ct);
    }

    private async Task HandleRemoveTaskAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument,
        CancellationToken ct)
    {
        var taskId = ParseTaskId(argument, "/removetask");
        var task = await FindUserTaskAsync(user.UserId, taskId, ct);
        await _toDoService.DeleteAsync(taskId, ct);
        await botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" удалена.", ct);
    }

    private async Task HandleReportAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        CancellationToken ct)
    {
        var (total, completed, active, generatedAt) = await _reportService.GetUserStatsAsync(user.UserId, ct);
        var text = string.Format(
            CultureInfo.InvariantCulture,
            "Статистика по задачам на {0}. Всего: {1}; Завершенных: {2}; Активных: {3};",
            generatedAt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            total,
            completed,
            active);
        await botClient.SendMessage(update.Message.Chat, text, ct);
    }

    private async Task HandleFindAsync(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Укажите начало названия задачи. Пример: /find Важное");
        }

        var tasks = await _toDoService.FindAsync(user, argument, ct);
        var text = tasks.Count == 0
            ? "Задачи с указанным началом названия не найдены."
            : FormatTasks("Найденные задачи:", tasks, includeState: false);
        await botClient.SendMessage(update.Message.Chat, text, ct);
    }

    private async Task<ToDoItem> FindUserTaskAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        var tasks = await _toDoService.GetAllByUserIdAsync(userId, ct);
        var task = tasks.FirstOrDefault(item => item.Id == taskId);
        return task ?? throw new InvalidOperationException(
            $"Задача с Id {taskId} не найдена у текущего пользователя.");
    }

    private static Guid ParseTaskId(string? argument, string command)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException(
                $"Укажите Id задачи. Пример: {command} 73c7940a-ca8c-4327-8a15-9119bffd1d5e");
        }

        if (!Guid.TryParse(argument, out var taskId))
        {
            throw new ArgumentException($"Некорректный Id задачи: {argument}.");
        }

        return taskId;
    }

    private static (string Command, string? Argument) ParseCommand(string messageText)
    {
        var firstSpaceIndex = messageText.IndexOf(' ');
        if (firstSpaceIndex < 0)
        {
            return (messageText.ToLowerInvariant(), null);
        }

        var command = messageText[..firstSpaceIndex].ToLowerInvariant();
        var argument = messageText[(firstSpaceIndex + 1)..].Trim();
        return (command, argument.Length == 0 ? null : argument);
    }

    private static string FormatTasks(string header, IReadOnlyList<ToDoItem> tasks, bool includeState)
    {
        var builder = new StringBuilder(header);
        foreach (var task in tasks)
        {
            builder.AppendLine();
            if (includeState)
            {
                builder.Append('(').Append(task.State).Append(") ");
            }

            builder
                .Append(task.Name)
                .Append(" - ")
                .Append(task.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture))
                .Append(" - ")
                .Append(task.Id);
        }

        return builder.ToString();
    }

    private static string BuildExceptionMessage(Exception exception)
    {
        if (exception is ArgumentException
            or TaskCountLimitException
            or TaskLengthLimitException
            or DuplicateTaskException
            or InvalidOperationException)
        {
            return exception.Message;
        }

        return string.Join(
            Environment.NewLine,
            "Произошла непредвиденная ошибка:",
            $"Type: {exception.GetType().FullName}",
            $"Message: {exception.Message}",
            $"StackTrace: {exception.StackTrace ?? "отсутствует"}",
            $"InnerException: {exception.InnerException?.ToString() ?? "отсутствует"}");
    }
}
