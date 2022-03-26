using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using IHost host = Host.CreateDefaultBuilder(args).Build();
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

string botToken = config.GetValue<string>("TelegramBotToken");

var botClient = new TelegramBotClient(botToken);

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions{
    AllowedUpdates = {}
};

botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for {me.Username}...");
Console.WriteLine("Press any key to stop");
Console.ReadKey();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cts)
{
    //Console.WriteLine($"UpdateType: {update.Type}");

    switch(update.Type)
    {
        case UpdateType.Message:
            await HandleMessageAsync(botClient, update.Message!, cts);
            break;
        case UpdateType.CallbackQuery:
            await HandleCallbackQueryAsync(botClient, update.CallbackQuery!, cts);
            break;
        default:
            return;
    }
}

async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cts)
{
    if(callbackQuery == null)
        return;

    var chatId = callbackQuery.Message!.Chat.Id;
    var callbackData = callbackQuery.Data;

    switch(callbackData){
        case "location":
            await botClient.SendVenueAsync(
                chatId: chatId,
                replyMarkup: GetDefaultInlineKeyboardMarkup(1),
                latitude: 40.229625f,
                longitude: 44.545686f,
                title: "Канакер Парк здесь",
                address: "Канакер, улица Мелик Меликяна, тупик 3, ЖК Канакер Парк, Ереван, Армения",
                cancellationToken: cts);

            break;
        case "showvideo":
            {
                string videoPath = $"https://github.com/denis-shemenko/TGBot.KanakerPark/raw/master/assets/KanakerRender.mp4";

                await botClient.SendVideoAsync(
                    chatId,
                    new Telegram.Bot.Types.InputFiles.InputOnlineFile(videoPath),
                    replyMarkup: GetDefaultInlineKeyboardMarkup(3),
                    cancellationToken: cts
                );

                break;
            }
        case "showphoto":
            {
                var random = new Random();
                var imageNum = random.Next(1, 12).ToString().PadLeft(3, '0');
                string photoPath = $"https://github.com/denis-shemenko/TGBot.KanakerPark/raw/master/assets/{imageNum}.jpg";

                await botClient.SendPhotoAsync(
                    chatId,
                    new Telegram.Bot.Types.InputFiles.InputOnlineFile(photoPath),
                    replyMarkup: GetDefaultInlineKeyboardMarkup(4),
                    cancellationToken: cts
                );

                break;
            }
        case "getprice":
            // TODO!
            break;
        default:
            return;
    }
}

async Task HandleMessageAsync(ITelegramBotClient botClient, Message updateMessage, CancellationToken cts)
{
    if(updateMessage == null)
        return;
    
    var chatId = updateMessage.Chat.Id;
    var messageText = updateMessage.Text;

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}");

    switch(updateMessage.Type){
        case MessageType.Text:
            string introText = System.IO.File.ReadAllText("assets/Intro.txt");

            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: introText,
                replyMarkup: GetDefaultInlineKeyboardMarkup(),
                cancellationToken: cts
            );
            break;
        default:
            return;
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cts)
{
    var ErrorMessage = exception switch {
        ApiRequestException apiRequestException 
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n[{apiRequestException.Message}]",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

InlineKeyboardMarkup GetDefaultInlineKeyboardMarkup(int? initiatorBtn = null)
{
    var firstRow = new List<InlineKeyboardButton>(2);
    var secondRow = new List<InlineKeyboardButton>(2);

    firstRow.Add(InlineKeyboardButton.WithCallbackData(text: "Где находится?", callbackData: "location"));
    firstRow.Add(InlineKeyboardButton.WithCallbackData(text: "Рассчитать", callbackData: "getprice"));

    secondRow.Add(InlineKeyboardButton.WithCallbackData(text: "Показать видео", callbackData: "showvideo"));
    secondRow.Add(InlineKeyboardButton.WithCallbackData(text: "Показать фото", callbackData: "showphoto"));

    if(initiatorBtn.HasValue){
        switch(initiatorBtn.Value){
            case 1:
                firstRow.RemoveAt(0);
                break;
            case 3:
                secondRow.RemoveAt(0);
                break;
            case 4:
                secondRow[1].Text = "Еще фото";
                break;
            default:
                break;
        }
    }

    var kbMarkup = new InlineKeyboardMarkup(new []
    {
        firstRow,
        secondRow
    });

    return kbMarkup;
}