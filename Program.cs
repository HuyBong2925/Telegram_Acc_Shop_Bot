using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static Dictionary<long, int> userCoins = new(); // ChatId -> coins
    private static int stockCount = 5; // Số acc trong kho
    private static int pricePerAcc = 15000;

    // Mã giao dịch giả lập: code -> (chatId, coinAmount)
    private static Dictionary<string, (long chatId, int amount)> pendingPayments = new();

    static async Task Main(string[] args)
    {
        var botClient = new TelegramBotClient(""); // Thay token

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"🤖 Bot @{me.Username} đang chạy. Nhấn Enter để thoát.");

        Console.ReadLine();
        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: var messageText, Chat.Id: var chatId }) return;

        if (!userCoins.ContainsKey(chatId))
            userCoins[chatId] = 0;

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "Nạp Coin", "Check Số Dư" },
            new KeyboardButton[] { "Mua Acc", "LH Admin" }
        })
        {
            ResizeKeyboard = true
        };

        // Kiểm tra xem user gửi mã giao dịch chưa (mã có trong pendingPayments)
        if (pendingPayments.ContainsKey(messageText))
        {
            var payment = pendingPayments[messageText];
            if (payment.chatId == chatId)
            {
                userCoins[chatId] += payment.amount;
                pendingPayments.Remove(messageText);
                await botClient.SendTextMessageAsync(chatId,
                    $"✅ Thanh toán thành công! Số dư mới của bạn là {userCoins[chatId]} coins.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId,
                    "Mã giao dịch không hợp lệ hoặc không thuộc về bạn.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            return;
        }

        switch (messageText)
        {
            case "/start":
                await botClient.SendTextMessageAsync(chatId,
                    $"Xin chào! UID của bạn là {chatId}\n" +
                    $"Số dư hiện tại: {userCoins[chatId]} coins\n" +
                    $"Trong kho còn: {stockCount} cái\n" +
                    $"Giá 1 cái: {pricePerAcc}\n" +
                    "Mời bạn chọn chức năng",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;

            case "Nạp Coin":
                string paymentCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                int coinToAdd = 10000; // Ví dụ nạp 10,000 coins
                pendingPayments[paymentCode] = (chatId, coinToAdd);
                string fakePaymentLink = $"https://fakepay.com/pay?code={paymentCode}&amount={coinToAdd}";

                await botClient.SendTextMessageAsync(chatId,
                    $"Để nạp {coinToAdd} coins, vui lòng thanh toán tại: {fakePaymentLink}\n" +
                    $"Sau khi thanh toán, hãy gửi lại mã giao dịch (Payment Code) này cho tôi:\n{paymentCode}",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;

            case "Check Số Dư":
                await botClient.SendTextMessageAsync(chatId,
                    $"UID: {chatId}\nSố dư của bạn hiện tại là: {userCoins[chatId]} coins.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;

            case "Mua Acc":
                if (stockCount <= 0)
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "Xin lỗi, hiện kho đã hết hàng. Vui lòng quay lại sau.",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                    break;
                }

                if (userCoins[chatId] < pricePerAcc)
                {
                    await botClient.SendTextMessageAsync(chatId,
                        $"Bạn không đủ coins để mua acc.\nGiá: {pricePerAcc} coins\n" +
                        "Vui lòng nạp thêm coins.",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                    break;
                }

                // Trừ coins và giảm kho
                userCoins[chatId] -= pricePerAcc;
                stockCount--;

                await botClient.SendTextMessageAsync(chatId,
                    $"🎉 Bạn đã mua thành công 1 acc với giá {pricePerAcc} coins.\n" +
                    $"Số dư hiện tại: {userCoins[chatId]} coins.\n" +
                    $"Số acc còn lại trong kho: {stockCount}.\n" +
                    "Admin sẽ liên hệ gửi thông tin tài khoản cho bạn.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;

            case "LH Admin":
                await botClient.SendTextMessageAsync(chatId,
                    "Bạn có thể liên hệ Admin qua @AdminUserName hoặc số điện thoại 0123456789.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;

            default:
                await botClient.SendTextMessageAsync(chatId,
                    "Xin chào! Vui lòng chọn chức năng bên dưới.",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Lỗi: {exception.Message}");
        return Task.CompletedTask;
    }
}
