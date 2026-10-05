using System.Reflection;
using MBot;
using MBot.DataAccess;
using MBot.Entities;
using MBot.Exceptions;
using MBot.Infrastructure.DataAccess;
using MBot.Services;
using MBot.TelegramBot;
using Otus.ToDoList.ConsoleBot;
using Otus.ToDoList.ConsoleBot.Types;

namespace MBot.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("ToDoUser создаётся с системными полями", ToDoUserIsInitialized),
            ("ToDoItem создаётся в состоянии Active", ToDoItemIsInitialized),
            ("UserService регистрирует и находит пользователя", UserServiceRegistersAndGetsUser),
            ("UserService не создаёт дубликат регистрации", UserServiceDoesNotDuplicateRegistration),
            ("InMemoryUserRepository реализует IUserRepository", UserRepositoryStoresUsers),
            ("ToDoService проверяет ограничения конструктора", ToDoServiceValidatesConstructorLimits),
            ("ParseAndValidateInt сохранён в сервисе", ParseAndValidateIntWorks),
            ("ValidateString сохранён в сервисе", ValidateStringWorks),
            ("ToDoService добавляет задачу пользователю", ToDoServiceAddsTask),
            ("ToDoService разделяет задачи разных пользователей", ToDoServiceSeparatesUsers),
            ("ToDoService возвращает только активные задачи", ToDoServiceReturnsOnlyActiveTasks),
            ("ToDoService завершает задачу", ToDoServiceMarksTaskCompleted),
            ("ToDoService удаляет задачу", ToDoServiceDeletesTask),
            ("ToDoService применяет лимит количества к пользователю", ToDoServiceEnforcesCountLimitPerUser),
            ("ToDoService проверяет длину задачи", ToDoServiceEnforcesLengthLimit),
            ("ToDoService запрещает дубликаты пользователя", ToDoServiceRejectsDuplicateTask),
            ("Одинаковые названия допустимы у разных пользователей", ToDoServiceAllowsSameNameForDifferentUsers),
            ("InMemoryToDoRepository реализует операции хранилища", ToDoRepositoryImplementsContract),
            ("ToDoReportService возвращает кортеж статистики", ReportServiceReturnsStatsTuple),
            ("ToDoService ищет задачи через репозиторий и лямбду", ToDoServiceFindsTasksByPrefix),
            ("UpdateHandler реализует IUpdateHandler", UpdateHandlerImplementsInterface),
            ("Сервисы реализуют требуемые интерфейсы", ServicesImplementInterfaces),
            ("ToDoService не зависит от ITelegramBotClient", ToDoServiceDoesNotDependOnTelegramClient),
            ("/start регистрирует данные из Update.Message.From", StartRegistersUserFromUpdate),
            ("Повторный /start не создаёт нового пользователя", RepeatedStartKeepsExistingUser),
            ("/help доступен до регистрации", HelpIsAvailableBeforeRegistration),
            ("/info доступен до регистрации", InfoIsAvailableBeforeRegistration),
            ("/exit доступен до регистрации", ExitIsAvailableBeforeRegistration),
            ("/exit завершает консольный цикл", ExitStopsConsoleLoop),
            ("/exit корректно работает после регистрации", ExitAfterRegistration),
            ("/exit нечувствителен к регистру и пробелам", ExitAllowsWhitespaceAndDifferentCase),
            ("Команды задач недоступны до регистрации", TaskCommandsAreBlockedBeforeRegistration),
            ("Команда /echo удалена", EchoCommandIsRemoved),
            ("/addtask принимает имя задачи в той же команде", AddTaskUsesInlineTaskName),
            ("/addtask без имени отклоняется", AddTaskWithoutNameIsRejected),
            ("/showtasks показывает только Active", ShowTasksDisplaysOnlyActiveTasks),
            ("/showalltasks показывает все состояния", ShowAllTasksDisplaysEveryState),
            ("/completetask завершает задачу по Id", CompleteTaskMarksTaskCompleted),
            ("/removetask удаляет задачу по Id", RemoveTaskDeletesTaskById),
            ("Некорректный Id обрабатывается без падения", InvalidTaskIdIsHandled),
            ("Пользователь не может изменить чужую задачу", UserCannotModifyAnotherUsersTask),
            ("/help содержит новые форматы команд", HelpContainsNewCommandFormats),
            ("/report выводит статистику", ReportDisplaysUserStats),
            ("/find выводит задачи по началу названия", FindDisplaysTasksByPrefix)
        };

        var failures = 0;

        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"[FAIL] {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"\nПроверок: {tests.Length}; успешно: {tests.Length - failures}; ошибок: {failures}.");
        return failures == 0 ? 0 : 1;
    }

    private static void ToDoUserIsInitialized()
    {
        var before = DateTime.UtcNow;
        var user = new ToDoUser("test_user");
        var after = DateTime.UtcNow;

        AssertNotEqual(Guid.Empty, user.UserId, "UserId не должен быть пустым Guid.");
        AssertEqual("test_user", user.TelegramUserName, "TelegramUserName сохранён неверно.");
        AssertTrue(user.RegisteredAt >= before && user.RegisteredAt <= after, "RegisteredAt должен задаваться при создании пользователя.");
        AssertEqual(DateTimeKind.Utc, user.RegisteredAt.Kind, "RegisteredAt должен быть в UTC.");
    }

    private static void ToDoItemIsInitialized()
    {
        var user = CreateUser(1, "user");
        var before = DateTime.UtcNow;
        var item = new ToDoItem(user, "Проверить задание");
        var after = DateTime.UtcNow;

        AssertNotEqual(Guid.Empty, item.Id, "Id задачи не должен быть пустым Guid.");
        AssertTrue(ReferenceEquals(user, item.User), "В задаче должен храниться переданный пользователь.");
        AssertEqual("Проверить задание", item.Name, "Имя задачи сохранено неверно.");
        AssertTrue(item.CreatedAt >= before && item.CreatedAt <= after, "CreatedAt должен задаваться при создании задачи.");
        AssertEqual(ToDoItemState.Active, item.State, "Новая задача должна иметь состояние Active.");
        AssertEqual<DateTime?>(null, item.StateChangedAt, "StateChangedAt новой задачи должен быть null.");
    }

    private static void UserServiceRegistersAndGetsUser()
    {
        IUserService service = new UserService(new InMemoryUserRepository());
        var registered = service.RegisterUser(101, "console_user");
        var found = service.GetUser(101);

        AssertTrue(found is not null, "Пользователь должен находиться после регистрации.");
        AssertTrue(ReferenceEquals(registered, found), "GetUser должен вернуть зарегистрированный объект.");
        AssertEqual(101L, found!.TelegramUserId, "TelegramUserId найденного пользователя неверен.");
    }

    private static void UserServiceDoesNotDuplicateRegistration()
    {
        IUserService service = new UserService(new InMemoryUserRepository());
        var first = service.RegisterUser(101, "first_name");
        var second = service.RegisterUser(101, "second_name");

        AssertTrue(ReferenceEquals(first, second), "Повторная регистрация TelegramUserId не должна создавать новый объект.");
        AssertEqual("first_name", second.TelegramUserName, "Повторная регистрация не должна подменять существующего пользователя.");
    }

    private static void UserRepositoryStoresUsers()
    {
        IUserRepository repository = new InMemoryUserRepository();
        var user = CreateUser(101, "repository_user");
        repository.Add(user);

        AssertTrue(ReferenceEquals(user, repository.GetUser(user.UserId)), "GetUser должен находить пользователя по Guid.");
        AssertTrue(ReferenceEquals(user, repository.GetUserByTelegramUserId(101)), "GetUserByTelegramUserId должен находить пользователя.");
    }

    private static void ToDoServiceValidatesConstructorLimits()
    {
        AssertThrows<ArgumentException>(() => new ToDoService(new InMemoryToDoRepository(), 0, 10));
        AssertThrows<ArgumentException>(() => new ToDoService(new InMemoryToDoRepository(), 10, 101));
        _ = new ToDoService(new InMemoryToDoRepository(), 1, 100);
    }

    private static void ParseAndValidateIntWorks()
    {
        var service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        AssertEqual(50, service.ParseAndValidateInt("50", 1, 100), "Метод должен возвращать корректное число.");
        AssertThrows<ArgumentException>(() => service.ParseAndValidateInt("text", 1, 100));
        AssertThrows<ArgumentException>(() => service.ParseAndValidateInt("101", 1, 100));
    }

    private static void ValidateStringWorks()
    {
        var service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        service.ValidateString("Задача");
        AssertThrows<ArgumentException>(() => service.ValidateString("   "));
    }

    private static void ToDoServiceAddsTask()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var user = CreateUser(1, "user");
        var item = service.Add(user, "Новая задача");
        var items = service.GetAllByUserId(user.UserId);

        AssertEqual(1, items.Count, "Должна быть добавлена одна задача.");
        AssertTrue(ReferenceEquals(item, items[0]), "Сервис должен вернуть созданную задачу.");
        AssertEqual("Новая задача", item.Name, "Название задачи сохранено неверно.");
    }

    private static void ToDoServiceSeparatesUsers()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var firstUser = CreateUser(1, "first");
        var secondUser = CreateUser(2, "second");
        service.Add(firstUser, "Первая задача");
        service.Add(secondUser, "Вторая задача");

        var firstItems = service.GetAllByUserId(firstUser.UserId);
        var secondItems = service.GetAllByUserId(secondUser.UserId);

        AssertEqual(1, firstItems.Count, "Первый пользователь должен видеть только свою задачу.");
        AssertEqual("Первая задача", firstItems[0].Name, "Первому пользователю возвращена чужая задача.");
        AssertEqual(1, secondItems.Count, "Второй пользователь должен видеть только свою задачу.");
        AssertEqual("Вторая задача", secondItems[0].Name, "Второму пользователю возвращена чужая задача.");
    }

    private static void ToDoServiceReturnsOnlyActiveTasks()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var user = CreateUser(1, "user");
        var active = service.Add(user, "Активная");
        var completed = service.Add(user, "Завершённая");
        service.MarkCompleted(completed.Id);

        var activeItems = service.GetActiveByUserId(user.UserId);

        AssertEqual(1, activeItems.Count, "Должна вернуться одна активная задача.");
        AssertTrue(ReferenceEquals(active, activeItems[0]), "Возвращена неверная активная задача.");
    }

    private static void ToDoServiceMarksTaskCompleted()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var user = CreateUser(1, "user");
        var item = service.Add(user, "Завершить");
        var before = DateTime.UtcNow;

        service.MarkCompleted(item.Id);

        AssertEqual(ToDoItemState.Completed, item.State, "State должен стать Completed.");
        AssertTrue(item.StateChangedAt is not null, "StateChangedAt должен быть заполнен.");
        AssertTrue(item.StateChangedAt >= before, "StateChangedAt должен отражать момент завершения.");
    }

    private static void ToDoServiceDeletesTask()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var user = CreateUser(1, "user");
        var item = service.Add(user, "Удалить");

        service.Delete(item.Id);

        AssertEqual(0, service.GetAllByUserId(user.UserId).Count, "После Delete задача должна отсутствовать.");
    }

    private static void ToDoServiceEnforcesCountLimitPerUser()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 1, 100);
        var firstUser = CreateUser(1, "first");
        var secondUser = CreateUser(2, "second");
        service.Add(firstUser, "Первая");

        AssertThrows<TaskCountLimitException>(() => service.Add(firstUser, "Вторая"));
        _ = service.Add(secondUser, "Первая другого пользователя");
    }

    private static void ToDoServiceEnforcesLengthLimit()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 3);
        var user = CreateUser(1, "user");
        AssertThrows<TaskLengthLimitException>(() => service.Add(user, "1234"));
    }

    private static void ToDoServiceRejectsDuplicateTask()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var user = CreateUser(1, "user");
        service.Add(user, "Одинаковая");
        AssertThrows<DuplicateTaskException>(() => service.Add(user, "Одинаковая"));
    }

    private static void ToDoServiceAllowsSameNameForDifferentUsers()
    {
        IToDoService service = new ToDoService(new InMemoryToDoRepository(), 10, 100);
        var firstUser = CreateUser(1, "first");
        var secondUser = CreateUser(2, "second");
        service.Add(firstUser, "Общее название");
        service.Add(secondUser, "Общее название");

        AssertEqual(1, service.GetAllByUserId(firstUser.UserId).Count, "У первого пользователя задача должна сохраниться.");
        AssertEqual(1, service.GetAllByUserId(secondUser.UserId).Count, "У второго пользователя задача должна сохраниться.");
    }

    private static void ToDoRepositoryImplementsContract()
    {
        IToDoRepository repository = new InMemoryToDoRepository();
        var user = CreateUser(1, "user");
        var active = new ToDoItem(user, "Активная");
        var completed = new ToDoItem(user, "Завершенная")
        {
            State = ToDoItemState.Completed,
            StateChangedAt = DateTime.UtcNow
        };
        repository.Add(active);
        repository.Add(completed);

        AssertEqual(2, repository.GetAllByUserId(user.UserId).Count, "Репозиторий должен вернуть все задачи пользователя.");
        AssertEqual(1, repository.GetActiveByUserId(user.UserId).Count, "Репозиторий должен вернуть только активные задачи.");
        AssertTrue(repository.ExistsByName(user.UserId, "Активная"), "ExistsByName должен находить задачу пользователя.");
        AssertEqual(1, repository.CountActive(user.UserId), "CountActive должен считать только активные задачи.");
        AssertEqual(1, repository.Find(user.UserId, item => item.Name.StartsWith("Зав", StringComparison.Ordinal)).Count, "Find должен применять переданный предикат.");

        repository.Delete(active.Id);
        AssertEqual(1, repository.GetAllByUserId(user.UserId).Count, "Delete должен удалять задачу по Id.");
    }

    private static void ReportServiceReturnsStatsTuple()
    {
        var repository = new InMemoryToDoRepository();
        var service = new ToDoService(repository, 10, 100);
        var reportService = new ToDoReportService(repository);
        var user = CreateUser(1, "user");
        _ = service.Add(user, "Активная");
        var completed = service.Add(user, "Завершенная");
        service.MarkCompleted(completed.Id);
        var before = DateTime.UtcNow;

        var (total, completedCount, active, generatedAt) = reportService.GetUserStats(user.UserId);

        AssertEqual(2, total, "Общее количество задач неверно.");
        AssertEqual(1, completedCount, "Количество завершенных задач неверно.");
        AssertEqual(1, active, "Количество активных задач неверно.");
        AssertTrue(generatedAt >= before, "Время формирования отчета должно быть установлено.");
    }

    private static void ToDoServiceFindsTasksByPrefix()
    {
        var repository = new InMemoryToDoRepository();
        IToDoService service = new ToDoService(repository, 10, 100);
        var user = CreateUser(1, "user");
        _ = service.Add(user, "Купить молоко");
        var expected = service.Add(user, "Купить хлеб");
        _ = service.Add(user, "Позвонить");

        var found = service.Find(user, "купить");

        AssertEqual(2, found.Count, "Find должен возвращать задачи с указанным началом названия.");
        AssertTrue(found.Contains(expected), "Find не вернул подходящую задачу.");
    }

    private static void UpdateHandlerImplementsInterface()
    {
        AssertTrue(typeof(IUpdateHandler).IsAssignableFrom(typeof(UpdateHandler)), "UpdateHandler должен реализовывать IUpdateHandler.");
    }

    private static void ServicesImplementInterfaces()
    {
        AssertTrue(typeof(IUserService).IsAssignableFrom(typeof(UserService)), "UserService должен реализовывать IUserService.");
        AssertTrue(typeof(IToDoService).IsAssignableFrom(typeof(ToDoService)), "ToDoService должен реализовывать IToDoService.");
    }

    private static void ToDoServiceDoesNotDependOnTelegramClient()
    {
        var fields = typeof(ToDoService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        var hasTelegramClientField = fields.Any(field => typeof(ITelegramBotClient).IsAssignableFrom(field.FieldType));
        var constructorParameters = typeof(ToDoService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        AssertFalse(hasTelegramClientField, "ToDoService не должен хранить ITelegramBotClient.");
        AssertFalse(constructorParameters.Any(type => typeof(ITelegramBotClient).IsAssignableFrom(type)), "ToDoService не должен получать ITelegramBotClient через конструктор.");
    }

    private static void StartRegistersUserFromUpdate()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(9001, "from_update", "/start"));
        var user = context.UserService.GetUser(9001);

        AssertTrue(user is not null, "/start должен зарегистрировать пользователя.");
        AssertEqual(9001L, user!.TelegramUserId, "TelegramUserId должен браться из Update.Message.From.Id.");
        AssertEqual("from_update", user.TelegramUserName, "TelegramUserName должен браться из Update.Message.From.Username.");
        AssertContains(context.BotClient.LastMessage, "Регистрация выполнена");
    }

    private static void RepeatedStartKeepsExistingUser()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(9001, "first_name", "/start"));
        var first = context.UserService.GetUser(9001);
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(9001, "changed_name", "/start"));
        var second = context.UserService.GetUser(9001);

        AssertTrue(first is not null && ReferenceEquals(first, second), "Повторный /start не должен создавать нового пользователя.");
        AssertContains(context.BotClient.LastMessage, "уже зарегистрирован");
    }

    private static void HelpIsAvailableBeforeRegistration()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "guest", "/help"));

        AssertContains(context.BotClient.LastMessage, "/start");
        AssertContains(context.BotClient.LastMessage, "/info");
        AssertContains(context.BotClient.LastMessage, "сначала выполните /start");
    }

    private static void InfoIsAvailableBeforeRegistration()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "guest", "/info"));
        AssertContains(context.BotClient.LastMessage, "Бот для управления списком задач");
    }

    private static void TaskCommandsAreBlockedBeforeRegistration()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "guest", "/addtask Нельзя добавить"));

        AssertContains(context.BotClient.LastMessage, "Пользователь не зарегистрирован");
        AssertEqual(0, context.ToDoService.GetAllByUserId(Guid.NewGuid()).Count, "Команда до регистрации не должна создавать задачу.");
    }

    private static void EchoCommandIsRemoved()
    {
        var context = CreateContext();
        Register(context, 10, "user");
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/echo test"));

        AssertContains(context.BotClient.LastMessage, "Неизвестная команда");
    }

    private static void AddTaskUsesInlineTaskName()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/addtask Новая задача с пробелами"));
        var tasks = context.ToDoService.GetAllByUserId(user.UserId);

        AssertEqual(1, tasks.Count, "Команда должна добавить одну задачу.");
        AssertEqual("Новая задача с пробелами", tasks[0].Name, "Название должно браться из аргумента /addtask.");
        AssertContains(context.BotClient.LastMessage, tasks[0].Id.ToString());
    }

    private static void AddTaskWithoutNameIsRejected()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/addtask"));

        AssertContains(context.BotClient.LastMessage, "Укажите название задачи");
        AssertEqual(0, context.ToDoService.GetAllByUserId(user.UserId).Count, "Пустая задача не должна добавляться.");
    }

    private static void ShowTasksDisplaysOnlyActiveTasks()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        var active = context.ToDoService.Add(user, "Активная");
        var completed = context.ToDoService.Add(user, "Завершённая");
        context.ToDoService.MarkCompleted(completed.Id);

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/showtasks"));

        AssertContains(context.BotClient.LastMessage, active.Name);
        AssertContains(context.BotClient.LastMessage, active.Id.ToString());
        AssertNotContains(context.BotClient.LastMessage, completed.Name);
    }

    private static void ShowAllTasksDisplaysEveryState()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        var active = context.ToDoService.Add(user, "Активная");
        var completed = context.ToDoService.Add(user, "Завершённая");
        context.ToDoService.MarkCompleted(completed.Id);

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/showalltasks"));

        AssertContains(context.BotClient.LastMessage, $"(Active) {active.Name}");
        AssertContains(context.BotClient.LastMessage, $"(Completed) {completed.Name}");
    }

    private static void CompleteTaskMarksTaskCompleted()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        var task = context.ToDoService.Add(user, "Завершить");

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", $"/completetask {task.Id}"));

        AssertEqual(ToDoItemState.Completed, task.State, "Команда должна вызвать завершение задачи.");
        AssertTrue(task.StateChangedAt is not null, "StateChangedAt должен обновиться.");
        AssertContains(context.BotClient.LastMessage, "отмечена как выполненная");
    }

    private static void RemoveTaskDeletesTaskById()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        var task = context.ToDoService.Add(user, "Удалить");

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", $"/removetask {task.Id}"));

        AssertEqual(0, context.ToDoService.GetAllByUserId(user.UserId).Count, "Задача должна быть удалена по Id.");
        AssertContains(context.BotClient.LastMessage, "удалена");
    }

    private static void InvalidTaskIdIsHandled()
    {
        var context = CreateContext();
        Register(context, 10, "user");
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/completetask wrong-id"));

        AssertContains(context.BotClient.LastMessage, "Некорректный Id задачи");
    }

    private static void UserCannotModifyAnotherUsersTask()
    {
        var context = CreateContext();
        var first = Register(context, 10, "first");
        var second = Register(context, 20, "second");
        var firstTask = context.ToDoService.Add(first, "Первая");
        _ = second;

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(20, "second", $"/removetask {firstTask.Id}"));

        AssertEqual(1, context.ToDoService.GetAllByUserId(first.UserId).Count, "Чужая задача не должна быть удалена.");
        AssertContains(context.BotClient.LastMessage, "не найдена у текущего пользователя");
    }

    private static void ExitIsAvailableBeforeRegistration()
    {
        var context = CreateContext();
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "guest", "/help"));
        AssertContains(context.BotClient.LastMessage, "/exit");
        AssertThrows<ExitSessionException>(() =>
            context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "guest", "/exit")));
        AssertContains(context.BotClient.LastMessage, "До свидания! Время текущего сеанса:");
    }

    private static void ExitStopsConsoleLoop()
    {
        var oldInput = Console.In;
        var oldOutput = Console.Out;
        using var input = new StringReader("/exit\n/start\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            var context = CreateContext();
            var counter = new CountingUpdateHandler(context.Handler);
            AssertThrows<ExitSessionException>(() => new ConsoleBotClient().StartReceiving(counter));
            AssertContains(output.ToString(), "До свидания! Время текущего сеанса:");
            AssertEqual(1, counter.Calls, "Команды после /exit не должны выполняться.");
        }
        finally
        {
            Console.SetIn(oldInput);
            Console.SetOut(oldOutput);
        }
    }

    private static void ExitAfterRegistration()
    {
        var context = CreateContext();
        Register(context, 10, "user");
        AssertThrows<ExitSessionException>(() =>
            context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/exit")));
        AssertContains(context.BotClient.LastMessage, "До свидания! Время текущего сеанса:");
    }

    private static void ExitAllowsWhitespaceAndDifferentCase()
    {
        var oldInput = Console.In;
        var oldOutput = Console.Out;
        using var input = new StringReader("  /EXIT  \n/start\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            var context = CreateContext();
            var counter = new CountingUpdateHandler(context.Handler);
            AssertThrows<ExitSessionException>(() => new ConsoleBotClient().StartReceiving(counter));
            AssertContains(output.ToString(), "До свидания! Время текущего сеанса:");
            AssertEqual(1, counter.Calls, "Команды после /EXIT не должны выполняться.");
        }
        finally
        {
            Console.SetIn(oldInput);
            Console.SetOut(oldOutput);
        }
    }

    private static void HelpContainsNewCommandFormats()
    {
        var context = CreateContext();
        Register(context, 10, "user");
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/help"));

        AssertContains(context.BotClient.LastMessage, "/addtask <название задачи>");
        AssertContains(context.BotClient.LastMessage, "/removetask <Id>");
        AssertContains(context.BotClient.LastMessage, "/completetask <Id>");
        AssertContains(context.BotClient.LastMessage, "/report");
        AssertContains(context.BotClient.LastMessage, "/find <начало названия>");
        AssertNotContains(context.BotClient.LastMessage, "/echo");
    }

    private static void ReportDisplaysUserStats()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        _ = context.ToDoService.Add(user, "Активная");
        var completed = context.ToDoService.Add(user, "Завершенная");
        context.ToDoService.MarkCompleted(completed.Id);

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/report"));

        AssertContains(context.BotClient.LastMessage, "Статистика по задачам на");
        AssertContains(context.BotClient.LastMessage, "Всего: 2");
        AssertContains(context.BotClient.LastMessage, "Завершенных: 1");
        AssertContains(context.BotClient.LastMessage, "Активных: 1");
    }

    private static void FindDisplaysTasksByPrefix()
    {
        var context = CreateContext();
        var user = Register(context, 10, "user");
        var matching = context.ToDoService.Add(user, "Сделать отчет");
        _ = context.ToDoService.Add(user, "Купить продукты");

        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(10, "user", "/find Сделать"));

        AssertContains(context.BotClient.LastMessage, matching.Name);
        AssertContains(context.BotClient.LastMessage, matching.Id.ToString());
        AssertNotContains(context.BotClient.LastMessage, "Купить продукты");
    }

    private static TestContext CreateContext(int taskCountLimit = 10, int taskLengthLimit = 100)
    {
        var userRepository = new InMemoryUserRepository();
        var toDoRepository = new InMemoryToDoRepository();
        var userService = new UserService(userRepository);
        var toDoService = new ToDoService(toDoRepository, taskCountLimit, taskLengthLimit);
        var reportService = new ToDoReportService(toDoRepository);
        var handler = new UpdateHandler(userService, toDoService, reportService);
        var botClient = new FakeTelegramBotClient();
        return new TestContext(userService, toDoService, handler, botClient);
    }

    private static ToDoUser CreateUser(long telegramUserId, string telegramUserName)
    {
        return new ToDoUser(telegramUserName)
        {
            TelegramUserId = telegramUserId
        };
    }

    private static ToDoUser Register(TestContext context, long telegramUserId, string telegramUserName)
    {
        context.Handler.HandleUpdateAsync(context.BotClient, CreateUpdate(telegramUserId, telegramUserName, "/start"));
        return context.UserService.GetUser(telegramUserId)
            ?? throw new InvalidOperationException("Пользователь не зарегистрирован тестовым /start.");
    }

    private static Update CreateUpdate(long telegramUserId, string telegramUserName, string text)
    {
        return new Update
        {
            Message = new Message
            {
                Id = 1,
                Text = text,
                Chat = new Chat { Id = telegramUserId + 1000 },
                From = new User
                {
                    Id = telegramUserId,
                    Username = telegramUserName
                }
            }
        };
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Ожидалось: {expected}; получено: {actual}.");
        }
    }

    private static void AssertNotEqual<T>(T notExpected, T actual, string message)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string actual, string expected)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Строка не содержит ожидаемый фрагмент: {expected}");
        }
    }

    private static void AssertNotContains(string actual, string expected)
    {
        if (actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Строка содержит нежелательный фрагмент: {expected}");
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Ожидалось исключение {typeof(TException).Name}.");
    }

    private sealed record TestContext(
        UserService UserService,
        ToDoService ToDoService,
        UpdateHandler Handler,
        FakeTelegramBotClient BotClient);

    private sealed class CountingUpdateHandler : IUpdateHandler
    {
        private readonly IUpdateHandler _handler;

        public CountingUpdateHandler(IUpdateHandler handler)
        {
            _handler = handler;
        }

        public int Calls { get; private set; }

        public void HandleUpdateAsync(ITelegramBotClient botClient, Update update)
        {
            Calls++;
            _handler.HandleUpdateAsync(botClient, update);
        }
    }

    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        private readonly List<string> _messages = new();

        public string LastMessage => _messages.Count == 0 ? string.Empty : _messages[^1];

        public void StartReceiving(IUpdateHandler handler)
        {
            throw new NotSupportedException("StartReceiving не используется в модульных проверках.");
        }

        public void SendMessage(Chat chat, string text)
        {
            ArgumentNullException.ThrowIfNull(chat);
            ArgumentNullException.ThrowIfNull(text);
            _messages.Add(text);
        }
    }
}
