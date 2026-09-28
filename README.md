# MBot: репозитории, отчёты и поиск

Домашнее задание по расширению Telegram To-Do бота на C# / .NET 8.

## Реализовано

- контракты `IUserRepository` и `IToDoRepository`;
- in-memory реализации репозиториев на основе `List`;
- передача репозиториев в `UserService` и `ToDoService` через конструкторы;
- `IToDoReportService` и `ToDoReportService`, возвращающий кортеж статистики;
- команда `/report` со статистикой задач пользователя;
- поиск `/find <начало названия>` через `Func<ToDoItem, bool>`;
- обновлённая команда `/help`;
- встроенный набор из 40 проверок, включая изоляцию данных пользователей.

## Структура

`src/MBot/DataAccess` содержит интерфейсы репозиториев, а
`src/MBot/Infrastructure/DataAccess` - их in-memory реализации. Сервисы
расположены в `src/MBot` и `src/MBot/Services`.

## Сборка и проверка

```powershell
dotnet build .\MBot.sln
dotnet run --project .\tests\MBot.Tests\MBot.Tests.csproj --no-build --no-restore
```
