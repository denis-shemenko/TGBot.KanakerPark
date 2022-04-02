using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

const string ApartmentDataPrefix = "apart_";
const string FirstPayPrefix = "pay_";

List<float> AvailableFirstPaymentPercents = new List<float>{ 60, 70, 80, 90 };

using IHost host = Host.CreateDefaultBuilder(args).Build();
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

var logger = LogManager.GetCurrentClassLogger();
float _selectedSquare = 0;
float _selectedReminder = 0;
float _selectedFirstPaymentPercent = 0;
float _selectedFirstPayment = 0;
int _currentPhotoNum = 1;

float costPerSquareMeter = config.GetValue<float>("CostPerSquareMeter");
string botToken = config.GetValue<string>("TelegramBotToken");
var botClient = new TelegramBotClient(botToken);
using var cts = new CancellationTokenSource();
var receiverOptions = new ReceiverOptions{
    AllowedUpdates = new [] { UpdateType.Message, UpdateType.CallbackQuery }
};

botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for {me.Username}...");
Console.WriteLine("Press any key to stop");
Console.ReadKey();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cts)
{
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

    if(callbackData!.StartsWith(ApartmentDataPrefix)){
        _selectedSquare = float.Parse(callbackData.Replace(ApartmentDataPrefix, ""));

        Message sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "🏦 Выберите первоначальный взнос",
            replyMarkup: GetFirstPaymentKeyboardMarkup(),
            cancellationToken: cts
        );

        return;
    }

    if(callbackData!.StartsWith(FirstPayPrefix)){
        _selectedFirstPaymentPercent = float.Parse(callbackData.Replace(FirstPayPrefix, ""));

        float selectedFullPrice = _selectedSquare * costPerSquareMeter;
        _selectedFirstPayment = selectedFullPrice * (_selectedFirstPaymentPercent / 100.0f);
        _selectedReminder = selectedFullPrice - _selectedFirstPayment;

        Message sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"💵 Первый взнос за квартиру {_selectedSquare} кв.м. составит {_selectedFirstPayment:n2} 💲",
            replyMarkup: GetAfterCalcKeyboardMarkup(),
            cancellationToken: cts
        );

        return;
    }

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
                // var random = new Random();
                // var imageNum = random.Next(1, 12).ToString().PadLeft(3, '0');
                var imageNum = _currentPhotoNum.ToString().PadLeft(3, '0');
                string photoPath = $"https://github.com/denis-shemenko/TGBot.KanakerPark/raw/master/assets/{imageNum}.jpg";

                int maxPhotoNum = Directory.GetFiles("assets", "*.jpg").Length;
                _currentPhotoNum++;
                if(_currentPhotoNum > maxPhotoNum)
                    _currentPhotoNum = 1;

                await botClient.SendPhotoAsync(
                    chatId,
                    new Telegram.Bot.Types.InputFiles.InputOnlineFile(photoPath),
                    replyMarkup: GetDefaultInlineKeyboardMarkup(4),
                    cancellationToken: cts
                );

                break;
            }
        case "backToApartments":
        case "getprice":
            {
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🏡 Выберите квартиру в наличии",
                    replyMarkup: GetApartmentsKeyboardMarkup(),
                    cancellationToken: cts
                );
            }
            break;
        case "paymentSchedule":
            {
                int monthsToPay = 23;
                float amountToPayMonthly = _selectedReminder / monthsToPay;
                float currentReminder = _selectedReminder;

                var sb = new StringBuilder();
                sb.AppendLine($"График платежей за квартиру {_selectedSquare}кв.м. при {_selectedFirstPaymentPercent}% первоначальном взносе:");
                sb.AppendLine(new string('-', 70));
                sb.AppendLine("Дата | Сумма платежа | Остаток долга");
                //sb.AppendLine($"{DateTime.Now.ToString("dd.MM.yyyy").PadRight(17, ' ')}| {_selectedFirstPayment.ToString("n2").PadRight(25, ' ')}| {currentReminder.ToString("n2")}");
                sb.AppendLine($"{DateTime.Now.ToString("dd.MM.yy")} | {_selectedFirstPayment.ToString("n2")} | {currentReminder.ToString("n2")}");

                for(int i = 1; i <= 23; i++){
                    currentReminder -= amountToPayMonthly;
                    //sb.AppendLine($"{DateTime.Now.AddMonths(i).ToString("dd.MM.yyyy").PadRight(17, ' ')}| {amountToPayMonthly.ToString("n2").PadRight(29, ' ')}| {currentReminder.ToString("n2")}");
                    sb.AppendLine($"{DateTime.Now.AddMonths(i).ToString("dd.MM.yy")} | {amountToPayMonthly.ToString("n2").PadRight(12, ' ')} | {currentReminder.ToString("n2")}");
                }
                sb.AppendLine(new string('-', 70));
                sb.AppendLine($"💵 Общая стоимость квартиры: {(_selectedSquare * costPerSquareMeter):n2} 💲");

                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: sb.ToString(),
                    replyMarkup: GetDefaultInlineKeyboardMarkup(),
                    cancellationToken: cts
                );

                break;                
            }
        case "backToMain":
            {
                string introText = System.IO.File.ReadAllText("assets/Intro.txt");

                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: introText,
                    replyMarkup: GetDefaultInlineKeyboardMarkup(),
                    cancellationToken: cts
                );

                break;
            }
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

    logger.Info($"Chat {chatId} started with User: {updateMessage.Chat.Username}");

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

    firstRow.Add(InlineKeyboardButton.WithCallbackData(text: "📍 Где находится?", callbackData: "location"));
    firstRow.Add(InlineKeyboardButton.WithCallbackData(text: "💰 Рассчитать", callbackData: "getprice"));

    secondRow.Add(InlineKeyboardButton.WithCallbackData(text: "🎞 Показать видео", callbackData: "showvideo"));
    secondRow.Add(InlineKeyboardButton.WithCallbackData(text: "📷 Показать фото", callbackData: "showphoto"));

    if(initiatorBtn.HasValue){
        switch(initiatorBtn.Value){
            case 1:
                firstRow.RemoveAt(0);
                break;
            case 3:
                secondRow.RemoveAt(0);
                break;
            case 4:
                secondRow[1].Text = "📷 Еще фото";
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

InlineKeyboardMarkup GetApartmentsKeyboardMarkup()
{
    var kbMarkup = new InlineKeyboardMarkup(new []
    {
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "45 кв.м.", callbackData: $"{ApartmentDataPrefix}45"),
            InlineKeyboardButton.WithCallbackData(text: "46 кв.м.", callbackData: $"{ApartmentDataPrefix}46"),
            InlineKeyboardButton.WithCallbackData(text: "52.5 кв.м.", callbackData: $"{ApartmentDataPrefix}52.5")
        },
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "53.5 кв.м.", callbackData: $"{ApartmentDataPrefix}53.5"),
            InlineKeyboardButton.WithCallbackData(text: "58 кв.м.", callbackData: $"{ApartmentDataPrefix}58"),
            InlineKeyboardButton.WithCallbackData(text: "59 кв.м.", callbackData: $"{ApartmentDataPrefix}59")
        },
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "63.5 кв.м.", callbackData: $"{ApartmentDataPrefix}63.5"),
            InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: $"backToMain")
        }
    });

    return kbMarkup;
}

InlineKeyboardMarkup GetFirstPaymentKeyboardMarkup()
{
    var kbMarkup = new InlineKeyboardMarkup(new []
    {
        AvailableFirstPaymentPercents.Select(
            fp => InlineKeyboardButton.WithCallbackData(text: $"{fp}%", callbackData: $"{FirstPayPrefix}{fp}")
        ),
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: $"backToApartments")
        }
    });

    return kbMarkup;
}

InlineKeyboardMarkup GetAfterCalcKeyboardMarkup()
{
    var kbMarkup = new InlineKeyboardMarkup(new []
    {
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "📈 График платежей на 2 года", callbackData: $"paymentSchedule")
        },
        new [] 
        {
            InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: $"backToApartments")
        }
    });

    return kbMarkup;
}