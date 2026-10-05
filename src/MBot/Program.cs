using MBot.TelegramBot;
using MBot.Exceptions;
using System.Text;
using MBot.DataAccess;
using MBot.Infrastructure.DataAccess;
using MBot.Services;
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

            if (!int.TryParse(taskCountLimitInput, out var taskCountLimit))
            {
                throw new ArgumentException(
                    $"Введите целое число от {ToDoService.MinimumLimit} до {ToDoService.MaximumLimit}.");
            }

            Console.WriteLine("Введите максимально допустимую длину задачи");
            var taskLengthLimitInput = Console.ReadLine();

            if (!int.TryParse(taskLengthLimitInput, out var taskLengthLimit))
            {
                throw new ArgumentException(
                    $"Введите целое число от {ToDoService.MinimumLimit} до {ToDoService.MaximumLimit}.");
            }

            IUserRepository userRepository = new InMemoryUserRepository();
            IToDoRepository toDoRepository = new InMemoryToDoRepository();
            IUserService userService = new UserService(userRepository);
            IToDoService toDoService = new ToDoService(toDoRepository, taskCountLimit, taskLengthLimit);
            IToDoReportService reportService = new ToDoReportService(toDoRepository);
            IUpdateHandler updateHandler = new UpdateHandler(userService, toDoService, reportService);
            ITelegramBotClient botClient = new ConsoleBotClient();

            botClient.StartReceiving(updateHandler);
        }
        catch (ExitSessionException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (exception is ArgumentException)
            {
                Console.WriteLine(exception.Message);
                return;
            }

            Console.WriteLine("Произошла непредвиденная ошибка:");
            Console.WriteLine($"Type: {exception.GetType().FullName}");
            Console.WriteLine($"Message: {exception.Message}");
            Console.WriteLine($"StackTrace: {exception.StackTrace ?? "отсутствует"}");
            Console.WriteLine($"InnerException: {exception.InnerException?.ToString() ?? "отсутствует"}");
        }
    }
}
