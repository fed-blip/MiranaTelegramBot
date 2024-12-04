using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


class Program
{
    static string botToken = "7185383879:AAHAfPttxzKQyMOVTzPPKJ0w_FfqPgWLopA";
    static Dictionary<long, HashSet<DateTime>> userMarkedDays = new();
    static Dictionary<long, List<string>> userNotes = new();
    static Dictionary<long, (DateTime, string)> userReminders = new();
    static async Task Main(string[] args)
    {
        var botClient = new TelegramBotClient(botToken);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };

        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);

        Console.WriteLine("Bot is running...");
        Console.ReadLine();

        cts.Cancel();
        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message) return;
        if (update.Message!.Type != MessageType.Text) return;

        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;

        switch (messageText)
        {
            case "/start":
                await botClient.SendTextMessageAsync(chatId,
                    "Вітаю🌞! Я допоможу тобі трекати звички та організувати нагадування.\n\n" +
                    "Список команд: /help", cancellationToken: cancellationToken);
                break;

            case "/help":
                await botClient.SendTextMessageAsync(chatId,
                    "Доступні команди:\n" +
                    "/track - Відмітити день без шкідливих звичок🔥\n" +
                    "/calendar - Переглянути календар із відмітками🥰\n" +
                    "/reminder - Налаштувати нагадування😴\n" +
                    "/notes - Додати або переглянути нотатки😋\n" +
                    "/deletenote - Видалити конкретну нотатку🤨\n" +
                    "/viewnotes - Переглянути всі нотатки🥴", cancellationToken: cancellationToken);
                break;

            case "/track":
                var today = DateTime.Today;
                if (!userMarkedDays.ContainsKey(chatId))
                    userMarkedDays[chatId] = new HashSet<DateTime>();

                userMarkedDays[chatId].Add(today);
                await botClient.SendTextMessageAsync(chatId, "День відмічено! Молодець!😎", cancellationToken: cancellationToken);
                break;

            case "/calendar":
                if (userMarkedDays.ContainsKey(chatId) && userMarkedDays[chatId].Count > 0)
                {
                    // Генерация изображения календаря
                    var calendarImage = GenerateCalendarImage(userMarkedDays[chatId]);

                    // Сохраняем изображение в поток
                    using var stream = new MemoryStream(calendarImage);

                    // Создаём InputOnlineFile с потоком
                    var inputFile = new InputFileStream(stream, "calendar.png");


                    // Отправляем фото
                    await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo: inputFile,
                        caption: "Ось твій календар:",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Немає відмічених днів.🤡", cancellationToken: cancellationToken);
                }
                break;

            case "/reminder":
                await botClient.SendTextMessageAsync(chatId,
                    "Вкажи дату та час у форматі🙄: yyyy-MM-dd HH:mm і текст нагадування.\nНаприклад: 2024-11-30 08:00 Прокинутися раніше😪",
                    cancellationToken: cancellationToken);
                break;

            case "/notes":
                if (!userNotes.ContainsKey(chatId)) userNotes[chatId] = new List<string>();

                await botClient.SendTextMessageAsync(chatId, "Введи текст нотатки або напиши /viewnotes, щоб переглянути всі нотатки.👼", cancellationToken: cancellationToken);
                break;

            case "/viewnotes":
                if (userNotes.ContainsKey(chatId) && userNotes[chatId].Count > 0)
                {
                    var notes = string.Join("\n", userNotes[chatId].Select((n, i) => $"{i + 1}. {n}"));
                    await botClient.SendTextMessageAsync(chatId, $"Твої нотатки:\n{notes}", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Нотаток немає.", cancellationToken: cancellationToken);
                }
                break;

            case "/deletenote":
                if (userNotes.ContainsKey(chatId) && userNotes[chatId].Count > 0)
                {
                    var notesToDelete = string.Join("\n", userNotes[chatId].Select((n, i) => $"{i + 1}. {n}"));
                    await botClient.SendTextMessageAsync(chatId, $"Введи номер нотатки, яку хочеш видалити:\n{notesToDelete}", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Немає нотаток для видалення.", cancellationToken: cancellationToken);
                }
                break;

            default:
                if (messageText.StartsWith("2024"))
                {
                    var parts = messageText.Split(' ', 2);
                    if (parts.Length == 2 && DateTime.TryParse(parts[0], out var reminderTime))
                    {
                        userReminders[chatId] = (reminderTime, parts[1]);
                        await botClient.SendTextMessageAsync(chatId, $"Нагадування налаштоване на {reminderTime}: {parts[1]}", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Неправильний формат. Спробуй ще раз.", cancellationToken: cancellationToken);
                    }
                }
                else if (int.TryParse(messageText, out int noteIndex))
                {
                    if (userNotes.ContainsKey(chatId) && noteIndex > 0 && noteIndex <= userNotes[chatId].Count)
                    {
                        userNotes[chatId].RemoveAt(noteIndex - 1);
                        await botClient.SendTextMessageAsync(chatId, "Нотатку видалено!", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Неправильний номер нотатки.", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    if (!userNotes.ContainsKey(chatId)) userNotes[chatId] = new List<string>();
                    userNotes[chatId].Add(messageText);
                    await botClient.SendTextMessageAsync(chatId, "Нотатку збережено!", cancellationToken: cancellationToken);
                }
                break;
        }

        // Перевірка нагадувань
        foreach (var reminder in userReminders.ToList())
        {
            if (DateTime.Now >= reminder.Value.Item1)
            {
                await botClient.SendTextMessageAsync(reminder.Key, $"Нагадування: {reminder.Value.Item2}", cancellationToken: cancellationToken);
                userReminders.Remove(reminder.Key);
            }
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }

    static byte[] GenerateCalendarImage(HashSet<DateTime> markedDays)
    {
        int width = 600;
        int height = 400;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
        };

        var markedPaint = new SKPaint
        {
            Color = SKColors.Red,
            TextSize = 20,
            IsAntialias = true,
        };

        // Заголовок
        paint.TextSize = 30;
        canvas.DrawText("Календар", 200, 40, paint);

        // Малюємо таблицю календаря
        int startX = 50;
        int startY = 80;
        int cellWidth = 70;
        int cellHeight = 50;

        var pen = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };

        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                var rect = new SKRect(startX + i * cellWidth, startY + j * cellHeight,
                                      startX + (i + 1) * cellWidth, startY + (j + 1) * cellHeight);
                canvas.DrawRect(rect, pen);
            }
        }

        // Дати
        var today = DateTime.Today;
        var firstDay = new DateTime(today.Year, today.Month, 1);
        int firstDayOffset = (int)firstDay.DayOfWeek;

        int currentRow = 0;
        int currentColumn = firstDayOffset;

        for (int day = 1; day <= DateTime.DaysInMonth(today.Year, today.Month); day++)
        {
            var date = new DateTime(today.Year, today.Month, day);
            var textPaint = markedDays.Contains(date) ? markedPaint : paint;

            float textX = startX + currentColumn * cellWidth + 10;
            float textY = startY + currentRow * cellHeight + 30;

            canvas.DrawText(day.ToString(), textX, textY, textPaint);

            currentColumn++;
            if (currentColumn > 6)
            {
                currentColumn = 0;
                currentRow++;
            }
        }

        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    
}
