using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yanzheng.Utils;

Config config = new()
{
    Proxy = "",
    Token = ""
};
if (!File.Exists("config.json"))
{
    File.WriteAllText("config.json", JsonSerializer.Serialize(config, new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    }));
}
config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"));

Dictionary<long, Dictionary<int, long>> data = new();
TelegramBotClient botClient = new(config.Token, string.IsNullOrWhiteSpace(config.Proxy)
    ? default
    : new(new HttpClientHandler
    {
        Proxy = new WebProxy(config.Proxy, true)
    }));
botClient.StartReceiving((_, update, _) =>
{
    if (update.Type is UpdateType.CallbackQuery)
    {
        _ = botClient.SendTextMessageAsync(update.CallbackQuery.From.Id, "已确认");
        _ = botClient.ApproveChatJoinRequest(data[update.CallbackQuery.From.Id][update.CallbackQuery.Message.MessageId], update.CallbackQuery.From.Id);
        data.Remove(update.CallbackQuery.From.Id);
        return;
    }
    if (update.Type is not UpdateType.ChatJoinRequest || !update.ChatJoinRequest.From.IsBot)
    {
        return;
    }
    if (!data.ContainsKey(update.CallbackQuery.From.Id))
    {
        data.Add(update.ChatJoinRequest.From.Id, new());
    }
    data[update.ChatJoinRequest.From.Id].Add(botClient.SendTextMessageAsync(update.ChatJoinRequest.From.Id, $"您正在加入{update.ChatJoinRequest.Chat.LastName}\n点击下方按钮确认您不是机器人", replyMarkup: new ReplyKeyboardMarkup(new[]
    {
        new KeyboardButton[] { "确认" },
    })).Result.MessageId, update.ChatJoinRequest.Chat.Id);
}, default);
