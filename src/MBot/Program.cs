using System.Text;
using MBot.DataAccess;
using MBot.Exceptions;
using MBot.Infrastructure.DataAccess;
using MBot.Services;
using MBot.TelegramBot;
using Otus.ToDoList.ConsoleBot;

namespace MBot;

internal static class Program
{
    private static void Main()
    {
        try
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Введите максимально допустимое количество задач");
            var taskCountLimitInput = Console.ReadLine();
            Console.WriteLine("Введите максимально допустимую длину задачи");
            var taskLengthLimitInput = Console.ReadLine();

            var validator = new ToDoService(new InMemoryToDoRepository(), 1, 1);
            var taskCountLimit = validator.ParseAndValidateInt(
                taskCountLimitInput,
                ToDoService.MinimumLimit,
                ToDoService.MaximumLimit);
            var taskLengthLimit = validator.ParseAndValidateInt(
                taskLengthLimitInput,
                ToDoService.MinimumLimit,
                ToDoService.MaximumLimit);

            IUserRepository userRepository = new InMemoryUserRepository();
            IToDoRepository toDoRepository = new InMemoryToDoRepository();
            IUserService userService = new UserService(userRepository);
            IToDoService toDoService = new ToDoService(toDoRepository, taskCountLimit, taskLengthLimit);
            IToDoReportService reportService = new ToDoReportService(toDoRepository);
            ITelegramBotClient botClient = new ConsoleBotClient();

            using var cts = new CancellationTokenSource();
            var updateHandler = new UpdateHandler(userService, toDoService, reportService, cts.Cancel);
            var originalInput = Console.In;
            var inputCoordinator = new ConsoleInputCoordinator(originalInput, cts.Token);
            updateHandler.UpdateProcessed += inputCoordinator.CompleteMessage;
            ConsoleCancelEventHandler interruptHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            try
            {
                Console.SetIn(inputCoordinator);
                Console.CancelKeyPress += interruptHandler;
                botClient.StartReceiving(updateHandler, cts.Token);
            }
            finally
            {
                Console.CancelKeyPress -= interruptHandler;
                Console.SetIn(originalInput);
                updateHandler.UpdateProcessed -= inputCoordinator.CompleteMessage;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine(exception.Message);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Произошла непредвиденная ошибка:");
            Console.WriteLine($"Type: {exception.GetType().FullName}");
            Console.WriteLine($"Message: {exception.Message}");
            Console.WriteLine($"StackTrace: {exception.StackTrace ?? "отсутствует"}");
            Console.WriteLine($"InnerException: {exception.InnerException?.ToString() ?? "отсутствует"}");
        }
    }
}
