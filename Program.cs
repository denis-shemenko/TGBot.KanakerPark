using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
    if(update.Type != UpdateType.Message)
        return;
    if(update.Message!.Type != MessageType.Text)
        return;

    var chatId = update.Message.Chat.Id;
    var messageText = update.Message.Text;

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}");

    // Message sentMessage = await botClient.SendTextMessageAsync(
    //     chatId: chatId,
    //     text: "Armen just said:\n" + messageText,
    //     cancellationToken: cts
    // );

    using var photoStream = new FileStream("assets/001.jpg", FileMode.Open);

    Message sentMessage = await botClient.SendPhotoAsync(
        chatId,
        new Telegram.Bot.Types.InputFiles.InputOnlineFile(photoStream, "001.jpg"),
        cancellationToken: cts
    );
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

// System.Console.WriteLine($"Hello Armenia! I am user {me.Id} and my name is {me.FirstName}");