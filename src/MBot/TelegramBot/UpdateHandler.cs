using MBot.Entities;
using MBot.Exceptions;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();

    public UpdateHandler(
        IUserService userService,
        IToDoService toDoService,
        IToDoReportService reportService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _toDoService = toDoService ?? throw new ArgumentNullException(nameof(toDoService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
    }

    public void HandleUpdateAsync(ITelegramBotClient botClient, Update update)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(botClient);
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
                    HandleStart(botClient, update);
                    return;
                case "/help":
                    HandleHelp(botClient, update);
                    return;
                case "/info":
                    HandleInfo(botClient, update);
                    return;
                case "/exit":
                    HandleExit(botClient, update);
                    return;
            }

            var user = _userService.GetUser(update.Message.From.Id);

            if (user is null)
            {
                botClient.SendMessage(
                    update.Message.Chat,
                    "Пользователь не зарегистрирован. Выполните /start. До регистрации доступны команды /help, /info и /exit.");
                return;
            }

            switch (command)
            {
                case "/addtask":
                    HandleAddTask(botClient, update, user, argument);
                    break;
                case "/showtasks":
                    HandleShowTasks(botClient, update, user);
                    break;
                case "/showalltasks":
                    HandleShowAllTasks(botClient, update, user);
                    break;
                case "/completetask":
                    HandleCompleteTask(botClient, update, user, argument);
                    break;
                case "/removetask":
                    HandleRemoveTask(botClient, update, user, argument);
                    break;
                case "/report":
                    HandleReport(botClient, update, user);
                    break;
                case "/find":
                    HandleFind(botClient, update, user, argument);
                    break;
                default:
                    botClient.SendMessage(
                        update.Message.Chat,
                        $"Неизвестная команда \"{command}\". Введите /help для просмотра справки.");
                    break;
            }
        }
        catch (ExitSessionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var chat = update?.Message?.Chat;

            if (chat is null || botClient is null)
            {
                return;
            }

            botClient.SendMessage(chat, BuildExceptionMessage(exception));
        }
    }

    private void HandleStart(ITelegramBotClient botClient, Update update)
    {
        var telegramUserId = update.Message.From.Id;
        var existingUser = _userService.GetUser(telegramUserId);

        if (existingUser is not null)
        {
            botClient.SendMessage(
                update.Message.Chat,
                $"Пользователь {existingUser.TelegramUserName} уже зарегистрирован. Введите /help для просмотра команд.");
            return;
        }

        var telegramUserName = string.IsNullOrWhiteSpace(update.Message.From.Username)
            ? $"user_{telegramUserId.ToString(CultureInfo.InvariantCulture)}"
            : update.Message.From.Username!;

        var user = _userService.RegisterUser(telegramUserId, telegramUserName);
        botClient.SendMessage(
            update.Message.Chat,
            $"Добрый день, {user.TelegramUserName}. Регистрация выполнена. Введите /help для просмотра команд.");
    }

    private void HandleExit(ITelegramBotClient botClient, Update update)
    {
        _sessionTimer.Stop();
        botClient.SendMessage(
            update.Message.Chat,
            $"До свидания! Время текущего сеанса: {FormatElapsed(_sessionTimer.Elapsed)}.");
        throw new ExitSessionException();
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

    private void HandleHelp(ITelegramBotClient botClient, Update update)
    {
        var isRegistered = _userService.GetUser(update.Message.From.Id) is not null;
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

        botClient.SendMessage(update.Message.Chat, builder.ToString());
    }

    private static void HandleInfo(ITelegramBotClient botClient, Update update)
    {
        botClient.SendMessage(
            update.Message.Chat,
            "Бот для управления списком задач. Команды обрабатываются через IUpdateHandler, данные пользователей и задач - через сервисные интерфейсы.");
    }

    private void HandleAddTask(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Укажите название задачи. Пример: /addtask Новая задача");
        }

        var task = _toDoService.Add(user, argument);
        botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" добавлена. Id: {task.Id}.");
    }

    private void HandleShowTasks(ITelegramBotClient botClient, Update update, ToDoUser user)
    {
        var tasks = _toDoService.GetActiveByUserId(user.UserId);

        if (tasks.Count == 0)
        {
            botClient.SendMessage(update.Message.Chat, "Список активных задач пуст.");
            return;
        }

        botClient.SendMessage(update.Message.Chat, FormatTasks("Активные задачи:", tasks, includeState: false));
    }

    private void HandleShowAllTasks(ITelegramBotClient botClient, Update update, ToDoUser user)
    {
        var tasks = _toDoService.GetAllByUserId(user.UserId);

        if (tasks.Count == 0)
        {
            botClient.SendMessage(update.Message.Chat, "Список задач пуст.");
            return;
        }

        botClient.SendMessage(update.Message.Chat, FormatTasks("Все задачи:", tasks, includeState: true));
    }

    private void HandleCompleteTask(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument)
    {
        var taskId = ParseTaskId(argument, "/completetask");
        var task = FindUserTask(user.UserId, taskId);

        if (task.State == ToDoItemState.Completed)
        {
            botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" уже выполнена.");
            return;
        }

        _toDoService.MarkCompleted(taskId);
        botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" отмечена как выполненная.");
    }

    private void HandleRemoveTask(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument)
    {
        var taskId = ParseTaskId(argument, "/removetask");
        var task = FindUserTask(user.UserId, taskId);
        _toDoService.Delete(taskId);
        botClient.SendMessage(update.Message.Chat, $"Задача \"{task.Name}\" удалена.");
    }

    private void HandleReport(ITelegramBotClient botClient, Update update, ToDoUser user)
    {
        var (total, completed, active, generatedAt) = _reportService.GetUserStats(user.UserId);
        var text = string.Format(
            CultureInfo.InvariantCulture,
            "Статистика по задачам на {0}. Всего: {1}; Завершенных: {2}; Активных: {3};",
            generatedAt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            total,
            completed,
            active);
        botClient.SendMessage(update.Message.Chat, text);
    }

    private void HandleFind(
        ITelegramBotClient botClient,
        Update update,
        ToDoUser user,
        string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Укажите начало названия задачи. Пример: /find Важное");
        }

        var tasks = _toDoService.Find(user, argument);
        if (tasks.Count == 0)
        {
            botClient.SendMessage(update.Message.Chat, "Задачи с указанным началом названия не найдены.");
            return;
        }

        botClient.SendMessage(update.Message.Chat, FormatTasks("Найденные задачи:", tasks, includeState: false));
    }

    private ToDoItem FindUserTask(Guid userId, Guid taskId)
    {
        var task = _toDoService
            .GetAllByUserId(userId)
            .FirstOrDefault(item => item.Id == taskId);

        if (task is null)
        {
            throw new InvalidOperationException($"Задача с Id {taskId} не найдена у текущего пользователя.");
        }

        return task;
    }

    private static Guid ParseTaskId(string? argument, string command)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException($"Укажите Id задачи. Пример: {command} 73c7940a-ca8c-4327-8a15-9119bffd1d5e");
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
