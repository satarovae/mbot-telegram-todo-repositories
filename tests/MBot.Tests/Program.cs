using System.Reflection;
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
    private static readonly CancellationToken Token = CancellationToken.None;

    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("ToDoUser: идентификатор, имя, время регистрации", () => Verify(() =>
            {
                var before = DateTime.UtcNow;
                var user = NewUser(1);
                Assert(user.UserId != Guid.Empty);
                Equal("user_1", user.TelegramUserName);
                Assert(user.RegisteredAt >= before && user.RegisteredAt <= DateTime.UtcNow);
                Equal(DateTimeKind.Utc, user.RegisteredAt.Kind);
            })),
            ("ToDoItem: первоначальное состояние Active", () => Verify(() =>
            {
                var user = NewUser(1);
                var item = new ToDoItem(user, "Сделать");
                Assert(ReferenceEquals(item.User, user));
                Equal(ToDoItemState.Active, item.State);
                Equal<DateTime?>(null, item.StateChangedAt);
                Assert(item.Id != Guid.Empty && item.CreatedAt.Kind == DateTimeKind.Utc);
            })),
            ("Все методы интерфейсов используют Task и CancellationToken", () => Verify(() =>
            {
                CheckAsyncMethods(typeof(IUserRepository));
                CheckAsyncMethods(typeof(IToDoRepository));
                CheckAsyncMethods(typeof(IUserService));
                CheckAsyncMethods(typeof(IToDoService));
                CheckAsyncMethods(typeof(IToDoReportService));
            })),
            ("Асинхронная библиотека предоставляет IUpdateHandler", () => Verify(() =>
            {
                var methods = typeof(IUpdateHandler).GetMethods();
                Equal(2, methods.Length);
                CheckAsyncMethods(typeof(IUpdateHandler), false);
                Assert(typeof(IUpdateHandler).IsAssignableFrom(typeof(UpdateHandler)));
            })),
            ("Асинхронная библиотека передаёт токен в SendMessage и StartReceiving", () => Verify(() =>
            {
                var send = typeof(ITelegramBotClient).GetMethod("SendMessage");
                var receive = typeof(ITelegramBotClient).GetMethod("StartReceiving");
                Assert(send is not null && receive is not null);
                Equal(typeof(Task), send!.ReturnType);
                Equal(typeof(CancellationToken), send.GetParameters()[^1].ParameterType);
                Equal(typeof(CancellationToken), receive!.GetParameters()[^1].ParameterType);
            })),
            ("Реализации соответствуют интерфейсам", () => Verify(() =>
            {
                Assert(typeof(IUserService).IsAssignableFrom(typeof(UserService)));
                Assert(typeof(IToDoService).IsAssignableFrom(typeof(ToDoService)));
                Assert(typeof(IToDoReportService).IsAssignableFrom(typeof(ToDoReportService)));
                Assert(typeof(IUserRepository).IsAssignableFrom(typeof(InMemoryUserRepository)));
                Assert(typeof(IToDoRepository).IsAssignableFrom(typeof(InMemoryToDoRepository)));
            })),
            ("ToDoService не зависит от Telegram-клиента", () => Verify(() =>
            {
                var fields = typeof(ToDoService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(fields.All(field => !typeof(ITelegramBotClient).IsAssignableFrom(field.FieldType)));
                Assert(typeof(ToDoService).GetConstructors().SelectMany(c => c.GetParameters())
                    .All(parameter => !typeof(ITelegramBotClient).IsAssignableFrom(parameter.ParameterType)));
            })),
            ("Проверяются ограничения конструктора", async () =>
            {
                await Throws<ArgumentException>(() => Verify(() => new ToDoService(new InMemoryToDoRepository(), 0, 10)));
                await Throws<ArgumentException>(() => Verify(() => new ToDoService(new InMemoryToDoRepository(), 10, 101)));
                await Throws<ArgumentNullException>(() => Verify(() => new ToDoService(null!, 1, 1)));
            }),
            ("Сохранён ParseAndValidateInt", () => Verify(() =>
            {
                var service = NewService();
                Equal(8, service.ParseAndValidateInt("8", 1, 100));
                ThrowsSync<ArgumentException>(() => service.ParseAndValidateInt("bad", 1, 100));
                ThrowsSync<ArgumentException>(() => service.ParseAndValidateInt("110", 1, 100));
                ThrowsSync<ArgumentException>(() => service.ParseAndValidateInt("3", 5, 2));
            })),
            ("Сохранён ValidateString", () => Verify(() =>
            {
                var service = NewService();
                service.ValidateString("Задача");
                ThrowsSync<ArgumentException>(() => service.ValidateString("  "));
            })),
            ("IUserRepository: добавление и поиск по двум идентификаторам", async () =>
            {
                var repo = new InMemoryUserRepository();
                var user = NewUser(123);
                await repo.AddAsync(user, Token);
                Assert(ReferenceEquals(user, await repo.GetUserAsync(user.UserId, Token)));
                Assert(ReferenceEquals(user, await repo.GetUserByTelegramUserIdAsync(123, Token)));
                Equal<ToDoUser?>(null, await repo.GetUserByTelegramUserIdAsync(999, Token));
            }),
            ("IUserRepository: исключения дублирования", async () =>
            {
                var repo = new InMemoryUserRepository();
                var user = NewUser(123);
                await repo.AddAsync(user, Token);
                await Throws<InvalidOperationException>(() => repo.AddAsync(user, Token));
                await Throws<InvalidOperationException>(() => repo.AddAsync(NewUser(123), Token));
            }),
            ("UserService регистрирует и находит пользователя", async () =>
            {
                IUserService service = new UserService(new InMemoryUserRepository());
                var user = await service.RegisterUserAsync(101, "login", Token);
                Assert(ReferenceEquals(user, await service.GetUserAsync(101, Token)));
                Equal("login", user.TelegramUserName);
            }),
            ("Повторная регистрация сохраняет пользователя", async () =>
            {
                var service = new UserService(new InMemoryUserRepository());
                var first = await service.RegisterUserAsync(101, "first", Token);
                var second = await service.RegisterUserAsync(101, "second", Token);
                Assert(ReferenceEquals(first, second));
            }),
            ("Проверка имени при регистрации", async () =>
            {
                var service = new UserService(new InMemoryUserRepository());
                await Throws<ArgumentException>(() => service.RegisterUserAsync(101, " ", Token));
            }),
            ("Конкурентная регистрация не создаёт дубликатов", async () =>
            {
                var service = new UserService(new InMemoryUserRepository());
                var users = await Task.WhenAll(Enumerable.Range(0, 20)
                    .Select(i => service.RegisterUserAsync(77, "user_" + i, Token)));
                Assert(users.All(user => ReferenceEquals(user, users[0])));
            }),
            ("Добавление задач через сервис", async () =>
            {
                var service = NewService();
                var user = NewUser(1);
                var item = await service.AddAsync(user, "Задача", Token);
                var all = await service.GetAllByUserIdAsync(user.UserId, Token);
                Equal(1, all.Count);
                Assert(ReferenceEquals(item, all[0]));
            }),
            ("Задачи разделены по пользователям", async () =>
            {
                var service = NewService();
                var a = NewUser(1);
                var b = NewUser(2);
                await service.AddAsync(a, "А", Token);
                await service.AddAsync(b, "Б", Token);
                Equal(1, (await service.GetAllByUserIdAsync(a.UserId, Token)).Count);
                Equal("Б", (await service.GetAllByUserIdAsync(b.UserId, Token))[0].Name);
            }),
            ("Сервис возвращает только активные задачи", async () =>
            {
                var service = NewService();
                var user = NewUser(1);
                var active = await service.AddAsync(user, "Активная", Token);
                var completed = await service.AddAsync(user, "Завершённая", Token);
                await service.MarkCompletedAsync(completed.Id, Token);
                var tasks = await service.GetActiveByUserIdAsync(user.UserId, Token);
                Equal(1, tasks.Count);
                Assert(ReferenceEquals(active, tasks[0]));
            }),
            ("Завершение задачи меняет статус и дату", async () =>
            {
                var service = NewService();
                var task = await service.AddAsync(NewUser(1), "Задача", Token);
                var before = DateTime.UtcNow;
                await service.MarkCompletedAsync(task.Id, Token);
                Equal(ToDoItemState.Completed, task.State);
                Assert(task.StateChangedAt >= before);
                var firstDate = task.StateChangedAt;
                await service.MarkCompletedAsync(task.Id, Token);
                Equal(firstDate, task.StateChangedAt);
            }),
            ("Удаление задачи и повторное удаление", async () =>
            {
                var service = NewService();
                var user = NewUser(1);
                var task = await service.AddAsync(user, "Убрать", Token);
                await service.DeleteAsync(task.Id, Token);
                Equal(0, (await service.GetAllByUserIdAsync(user.UserId, Token)).Count);
                await Throws<InvalidOperationException>(() => service.DeleteAsync(task.Id, Token));
            }),
            ("Лимит числа активных задач на пользователя", async () =>
            {
                var service = NewService(1);
                var a = NewUser(1);
                var b = NewUser(2);
                await service.AddAsync(a, "Первая", Token);
                await Throws<TaskCountLimitException>(() => service.AddAsync(a, "Вторая", Token));
                await service.AddAsync(b, "Чужая", Token);
            }),
            ("Лимит длины задачи", async () =>
            {
                var service = NewService(10, 3);
                await Throws<TaskLengthLimitException>(() => service.AddAsync(NewUser(1), "1234", Token));
            }),
            ("Дубликаты задач пользователя запрещены", async () =>
            {
                var service = NewService();
                var user = NewUser(1);
                await service.AddAsync(user, "Общая", Token);
                await Throws<DuplicateTaskException>(() => service.AddAsync(user, "Общая", Token));
            }),
            ("Одно название разрешено разным пользователям", async () =>
            {
                var service = NewService();
                await service.AddAsync(NewUser(1), "Название", Token);
                await service.AddAsync(NewUser(2), "Название", Token);
            }),
            ("Одновременное добавление не обходит ограничений", async () =>
            {
                var service = NewService(1);
                var user = NewUser(1);
                var operations = Enumerable.Range(0, 10).Select(async i =>
                {
                    try
                    {
                        await service.AddAsync(user, "Задача" + i, Token);
                        return true;
                    }
                    catch (TaskCountLimitException)
                    {
                        return false;
                    }
                });
                Equal(1, (await Task.WhenAll(operations)).Count(success => success));
            }),
            ("Репозиторий поддерживает CRUD и фильтрацию", async () =>
            {
                IToDoRepository repo = new InMemoryToDoRepository();
                var user = NewUser(1);
                var first = new ToDoItem(user, "Одна");
                var second = new ToDoItem(user, "Другая");
                await repo.AddAsync(first, Token);
                await repo.AddAsync(second, Token);
                Assert(await repo.ExistsByNameAsync(user.UserId, "Одна", Token));
                Equal(2, (await repo.GetAllByUserIdAsync(user.UserId, Token)).Count);
                Equal(2, await repo.CountActiveAsync(user.UserId, Token));
                Assert(ReferenceEquals(first, await repo.GetAsync(first.Id, Token)));
                second.State = ToDoItemState.Completed;
                await repo.UpdateAsync(second, Token);
                Equal(1, (await repo.GetActiveByUserIdAsync(user.UserId, Token)).Count);
                Equal(1, (await repo.FindAsync(user.UserId, t => t.Name.StartsWith("Д", StringComparison.Ordinal), Token)).Count);
                await repo.DeleteAsync(first.Id, Token);
                Equal(1, (await repo.GetAllByUserIdAsync(user.UserId, Token)).Count);
            }),
            ("Репозиторий защищает идентификаторы задач", async () =>
            {
                var repo = new InMemoryToDoRepository();
                var task = new ToDoItem(NewUser(1), "Повтор");
                await repo.AddAsync(task, Token);
                await Throws<InvalidOperationException>(() => repo.AddAsync(task, Token));
                await Throws<InvalidOperationException>(() => repo.DeleteAsync(Guid.NewGuid(), Token));
                await Throws<InvalidOperationException>(() => repo.UpdateAsync(new ToDoItem(NewUser(1), "Новая"), Token));
            }),
            ("Статистика возвращает кортеж", async () =>
            {
                var repo = new InMemoryToDoRepository();
                var service = new ToDoService(repo, 10, 100);
                var report = new ToDoReportService(repo);
                var user = NewUser(1);
                await service.AddAsync(user, "Активная", Token);
                var completed = await service.AddAsync(user, "Завершённая", Token);
                await service.MarkCompletedAsync(completed.Id, Token);
                var before = DateTime.UtcNow;
                var stats = await report.GetUserStatsAsync(user.UserId, Token);
                Equal(2, stats.total);
                Equal(1, stats.completed);
                Equal(1, stats.active);
                Assert(stats.generatedAt >= before);
            }),
            ("Поиск задач по префиксу без учёта регистра", async () =>
            {
                var service = NewService();
                var user = NewUser(1);
                await service.AddAsync(user, "Купить хлеб", Token);
                await service.AddAsync(user, "Купить воду", Token);
                await service.AddAsync(user, "Звонок", Token);
                Equal(2, (await service.FindAsync(user, "купить", Token)).Count);
            }),
            ("Асинхронные методы поддерживают отмену", async () =>
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                var userRepo = new InMemoryUserRepository();
                var taskRepo = new InMemoryToDoRepository();
                var user = NewUser(1);
                await Throws<OperationCanceledException>(() => userRepo.AddAsync(user, cts.Token));
                await Throws<OperationCanceledException>(() => userRepo.GetUserAsync(user.UserId, cts.Token));
                await Throws<OperationCanceledException>(() => userRepo.GetUserByTelegramUserIdAsync(1, cts.Token));
                await Throws<OperationCanceledException>(() => taskRepo.GetAllByUserIdAsync(user.UserId, cts.Token));
                await Throws<OperationCanceledException>(() => taskRepo.AddAsync(new ToDoItem(user, "x"), cts.Token));
                await Throws<OperationCanceledException>(() => new ToDoService(taskRepo, 10, 10).AddAsync(user, "x", cts.Token));
                await Throws<OperationCanceledException>(() => new UserService(userRepo).RegisterUserAsync(1, "x", cts.Token));
            }),
            ("Регистрация командой /start", async () =>
            {
                using var c = new Context();
                await Send(c, "/start");
                Assert(await c.Users.GetUserAsync(1, Token) is not null);
                Contains(c.Bot.LastMessage, "Регистрация выполнена");
            }),
            ("Повторный /start", async () =>
            {
                using var c = await Registered();
                await Send(c, "/start");
                Contains(c.Bot.LastMessage, "уже зарегистрирован");
            }),
            ("/start поддерживает отсутствие username", async () =>
            {
                using var c = new Context();
                await c.Handler.HandleUpdateAsync(c.Bot, NewUpdate(1, null, "/start"), Token);
                Equal("user_1", (await c.Users.GetUserAsync(1, Token))!.TelegramUserName);
            }),
            ("/help доступен до регистрации", async () =>
            {
                using var c = new Context();
                await Send(c, "/help");
                Contains(c.Bot.LastMessage, "/exit");
                Contains(c.Bot.LastMessage, "/start");
            }),
            ("/info доступен до регистрации", async () =>
            {
                using var c = new Context();
                await Send(c, "/info");
                Contains(c.Bot.LastMessage, "списком задач");
            }),
            ("Команды задач запрещены до регистрации", async () =>
            {
                using var c = new Context();
                await Send(c, "/addtask Дело");
                Contains(c.Bot.LastMessage, "не зарегистрирован");
            }),
            ("/echo отсутствует", async () =>
            {
                using var c = await Registered();
                await Send(c, "/echo тест");
                Contains(c.Bot.LastMessage, "Неизвестная команда");
            }),
            ("/addtask с именем в команде", async () =>
            {
                using var c = await Registered();
                await Send(c, "/addtask Купить продукты");
                Contains(c.Bot.LastMessage, "Купить продукты");
                Equal(1, (await c.Tasks.GetAllByUserIdAsync((await GetRegisteredUser(c)).UserId, Token)).Count);
            }),
            ("/addtask без имени", async () =>
            {
                using var c = await Registered();
                await Send(c, "/addtask");
                Contains(c.Bot.LastMessage, "Укажите название задачи");
            }),
            ("/showtasks: активные", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                await c.Tasks.AddAsync(user, "Активная", Token);
                var item = await c.Tasks.AddAsync(user, "Завершённая", Token);
                await c.Tasks.MarkCompletedAsync(item.Id, Token);
                await Send(c, "/showtasks");
                Contains(c.Bot.LastMessage, "Активная");
                NotContains(c.Bot.LastMessage, "Завершённая");
            }),
            ("/showalltasks: все состояния", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                var task = await c.Tasks.AddAsync(user, "Готово", Token);
                await c.Tasks.MarkCompletedAsync(task.Id, Token);
                await Send(c, "/showalltasks");
                Contains(c.Bot.LastMessage, "Completed");
                Contains(c.Bot.LastMessage, "Готово");
            }),
            ("/completetask завершает задачу по Id", async () =>
            {
                using var c = await Registered();
                var task = await c.Tasks.AddAsync(await GetRegisteredUser(c), "Готово", Token);
                await Send(c, $"/completetask {task.Id}");
                Equal(ToDoItemState.Completed, task.State);
            }),
            ("/completetask повторно сообщает о завершении", async () =>
            {
                using var c = await Registered();
                var task = await c.Tasks.AddAsync(await GetRegisteredUser(c), "Готово", Token);
                await Send(c, $"/completetask {task.Id}");
                await Send(c, $"/completetask {task.Id}");
                Contains(c.Bot.LastMessage, "уже выполнена");
            }),
            ("/removetask удаляет задачу по Id", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                var task = await c.Tasks.AddAsync(user, "Удалить", Token);
                await Send(c, $"/removetask {task.Id}");
                Equal(0, (await c.Tasks.GetAllByUserIdAsync(user.UserId, Token)).Count);
            }),
            ("Некорректный Id не вызывает падения", async () =>
            {
                using var c = await Registered();
                await Send(c, "/completetask bad-id");
                Contains(c.Bot.LastMessage, "Некорректный Id");
            }),
            ("Пользователь не изменяет чужую задачу", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                var task = await c.Tasks.AddAsync(user, "Не трогать", Token);
                await Send(c, "/start", 2, "second");
                await Send(c, $"/removetask {task.Id}", 2, "second");
                Contains(c.Bot.LastMessage, "не найдена у текущего пользователя");
                Equal(1, (await c.Tasks.GetAllByUserIdAsync(user.UserId, Token)).Count);
            }),
            ("/report показывает статистику", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                await c.Tasks.AddAsync(user, "Первая", Token);
                var completed = await c.Tasks.AddAsync(user, "Вторая", Token);
                await c.Tasks.MarkCompletedAsync(completed.Id, Token);
                await Send(c, "/report");
                Contains(c.Bot.LastMessage, "Всего: 2");
                Contains(c.Bot.LastMessage, "Завершенных: 1");
                Contains(c.Bot.LastMessage, "Активных: 1");
            }),
            ("/find выводит задачи по префиксу", async () =>
            {
                using var c = await Registered();
                var user = await GetRegisteredUser(c);
                await c.Tasks.AddAsync(user, "Купить сыр", Token);
                await c.Tasks.AddAsync(user, "Другое", Token);
                await Send(c, "/find КУПИТЬ");
                Contains(c.Bot.LastMessage, "Купить сыр");
                NotContains(c.Bot.LastMessage, "Другое");
            }),
            ("/find без аргумента", async () =>
            {
                using var c = await Registered();
                await Send(c, "/find");
                Contains(c.Bot.LastMessage, "Укажите начало названия");
            }),
            ("/help содержит все команды", async () =>
            {
                using var c = await Registered();
                await Send(c, "/help");
                foreach (var name in new[] { "/start", "/help", "/info", "/exit", "/addtask", "/showtasks",
                    "/showalltasks", "/completetask", "/removetask", "/report", "/find" })
                {
                    Contains(c.Bot.LastMessage, name);
                }
                NotContains(c.Bot.LastMessage, "/echo");
            }),
            ("/exit доступен без регистрации", async () =>
            {
                using var c = new Context();
                await Send(c, "/exit");
                Contains(c.Bot.LastMessage, "До свидания! Время текущего сеанса:");
                Assert(c.Cancellation.IsCancellationRequested);
            }),
            ("/exit работает после регистрации", async () =>
            {
                using var c = await Registered();
                await Send(c, "/exit");
                Assert(c.Cancellation.IsCancellationRequested);
            }),
            ("/exit допускает пробелы и другой регистр", async () =>
            {
                using var c = new Context();
                await Send(c, "  /EXIT   ");
                Assert(c.Cancellation.IsCancellationRequested);
            }),
            ("После /exit команды игнорируются", async () =>
            {
                using var c = new Context();
                await Send(c, "/exit");
                var last = c.Bot.LastMessage;
                await c.Handler.HandleUpdateAsync(c.Bot, NewUpdate(1, "user", "/start"), Token);
                Equal(last, c.Bot.LastMessage);
                Equal<ToDoUser?>(null, await c.Users.GetUserAsync(1, Token));
            }),
            ("После /exit не выдаются новые сообщения", async () =>
            {
                using var c = await Registered();
                await Send(c, "/exit");
                var count = c.Bot.Messages.Count;
                await c.Handler.HandleUpdateAsync(c.Bot, NewUpdate(1, "user", "/report"), Token);
                Equal(count, c.Bot.Messages.Count);
            }),
            ("Ошибки передаются в HandleErrorAsync", async () =>
            {
                using var c = new Context();
                var original = Console.Error;
                using var writer = new StringWriter();
                try
                {
                    Console.SetError(writer);
                    await c.Handler.HandleErrorAsync(c.Bot, new InvalidOperationException("ошибка из теста"), Token);
                }
                finally
                {
                    Console.SetError(original);
                }

                Contains(writer.ToString(), "ошибка из теста");
                Contains(writer.ToString(), "HandleError");
            }),
            ("Токен передаётся при отправке сообщений", async () =>
            {
                using var cts = new CancellationTokenSource();
                using var c = new Context();
                await c.Handler.HandleUpdateAsync(c.Bot, NewUpdate(1, "user", "/info"), cts.Token);
                Equal(cts.Token, c.Bot.LastToken);
            }),
            ("Отмена не превращается в ответ об ошибке", async () =>
            {
                using var cts = new CancellationTokenSource();
                using var c = new Context();
                cts.Cancel();
                await Throws<OperationCanceledException>(() => c.Handler.HandleUpdateAsync(
                    c.Bot, NewUpdate(1, "user", "/info"), cts.Token));
                Equal(0, c.Bot.Messages.Count);
            }),
            ("Событие UpdateProcessed происходит после ответа", async () =>
            {
                using var c = new Context();
                var completed = 0;
                c.Handler.UpdateProcessed += () =>
                {
                    Assert(c.Bot.Messages.Count > 0);
                    completed++;
                };
                await Send(c, "/help");
                await Send(c, "/info");
                Equal(2, completed);
            }),
            ("ConsoleInputCoordinator соблюдает порядок", async () =>
            {
                using var cts = new CancellationTokenSource();
                using var text = new StringReader("/start\n/exit\n");
                var input = new ConsoleInputCoordinator(text, cts.Token);
                Equal("/start", input.ReadLine());
                var waiting = Task.Run(input.ReadLine);
                await Task.Delay(20);
                Assert(!waiting.IsCompleted);
                input.CompleteMessage();
                Equal("/exit", await waiting.WaitAsync(TimeSpan.FromSeconds(2)));
                cts.Cancel();
                Equal<string?>(null, input.ReadLine());
            }),
            ("Официальный ConsoleBotClient обрабатывает /exit", async () =>
            {
                var originalInput = Console.In;
                var originalOutput = Console.Out;
                using var text = new StringReader("/start\n/addtask Тест\n/report\n/exit\n/start\n");
                using var output = new StringWriter();
                using var c = new Context();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(c.Cancellation.Token, timeout.Token);
                var input = new ConsoleInputCoordinator(text, linked.Token);
                c.Handler.UpdateProcessed += input.CompleteMessage;

                try
                {
                    Console.SetIn(input);
                    Console.SetOut(output);
                    await Task.Run(() => new ConsoleBotClient().StartReceiving(c.Handler, linked.Token));
                }
                finally
                {
                    Console.SetIn(originalInput);
                    Console.SetOut(originalOutput);
                    c.Handler.UpdateProcessed -= input.CompleteMessage;
                }

                Assert(!timeout.IsCancellationRequested, "Бот не завершился в течение пяти секунд.");
                Contains(output.ToString(), "Регистрация выполнена");
                Contains(output.ToString(), "Тест");
                Contains(output.ToString(), "Статистика по задачам");
                Contains(output.ToString(), "До свидания!");
                Contains(output.ToString(), "Бот остановлен");
                Assert(c.Cancellation.IsCancellationRequested);
            })
        };

        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                await run();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"[FAIL] {name}: {exception}");
            }
        }

        Console.WriteLine($"Проверок: {tests.Length}; успешно: {tests.Length - failures}; ошибок: {failures}.");
        return failures == 0 ? 0 : 1;
    }

    private static ToDoUser NewUser(long id) => new($"user_{id}") { TelegramUserId = id };

    private static ToDoService NewService(int countLimit = 10, int lengthLimit = 100)
        => new(new InMemoryToDoRepository(), countLimit, lengthLimit);

    private static Task Verify(Action check)
    {
        check();
        return Task.CompletedTask;
    }

    private static void CheckAsyncMethods(Type type, bool requireAsyncName = true)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert(methods.Length > 0);
        foreach (var method in methods)
        {
            if (requireAsyncName)
            {
                Assert(method.Name.EndsWith("Async", StringComparison.Ordinal), $"Метод {method.Name} должен иметь суффикс Async.");
            }

            Assert(typeof(Task).IsAssignableFrom(method.ReturnType), $"Метод {method.Name} должен возвращать Task.");
            Equal(typeof(CancellationToken), method.GetParameters()[^1].ParameterType);
        }
    }

    private static Update NewUpdate(long id, string? username, string text)
    {
        return new Update
        {
            Message = new Message
            {
                Id = 1,
                Text = text,
                From = new User { Id = id, Username = username },
                Chat = new Chat { Id = id + 1000 }
            }
        };
    }

    private static async Task Send(Context context, string command, long userId = 1, string? username = "user")
    {
        await context.Handler.HandleUpdateAsync(
            context.Bot,
            NewUpdate(userId, username, command),
            context.Cancellation.Token);
    }

    private static async Task<Context> Registered()
    {
        var context = new Context();
        await Send(context, "/start");
        return context;
    }

    private static async Task<ToDoUser> GetRegisteredUser(Context context)
    {
        return await context.Users.GetUserAsync(1, Token)
            ?? throw new InvalidOperationException("Пользователь не зарегистрирован.");
    }

    private static void Assert(bool value, string? message = null)
    {
        if (!value)
        {
            throw new InvalidOperationException(message ?? "Утверждение не выполнено.");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        Assert(EqualityComparer<T>.Default.Equals(expected, actual),
            $"Ожидалось: {expected}; получено: {actual}.");
    }

    private static void Contains(string actual, string expected)
    {
        Assert(actual.Contains(expected, StringComparison.Ordinal), $"Отсутствует: {expected}");
    }

    private static void NotContains(string actual, string expected)
    {
        Assert(!actual.Contains(expected, StringComparison.Ordinal), $"Лишняя подстрока: {expected}");
    }

    private static void ThrowsSync<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
    }

    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
    }

    private sealed class Context : IDisposable
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public IUserService Users { get; }
        public IToDoService Tasks { get; }
        public UpdateHandler Handler { get; }
        public FakeTelegramBotClient Bot { get; } = new();

        public Context()
        {
            var userRepo = new InMemoryUserRepository();
            var taskRepo = new InMemoryToDoRepository();
            Users = new UserService(userRepo);
            Tasks = new ToDoService(taskRepo, 10, 100);
            Handler = new UpdateHandler(Users, Tasks, new ToDoReportService(taskRepo), Cancellation.Cancel);
        }

        public void Dispose()
        {
            Cancellation.Dispose();
        }
    }

    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        public List<string> Messages { get; } = new();
        public string LastMessage => Messages.Count == 0 ? string.Empty : Messages[^1];
        public CancellationToken LastToken { get; private set; }

        public void StartReceiving(IUpdateHandler handler, CancellationToken ct)
        {
            throw new NotSupportedException("Используется метод HandleUpdateAsync.");
        }

        public Task SendMessage(Chat chat, string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(chat);
            ArgumentNullException.ThrowIfNull(text);
            LastToken = ct;
            Messages.Add(text);
            return Task.CompletedTask;
        }
    }
}
