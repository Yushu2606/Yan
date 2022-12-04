using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yanzheng.Utils;
using File = System.IO.File;
using Timer = System.Timers.Timer;

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

Dictionary<long, Dictionary<long, int>> data = new();
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
        if (
            (update.CallbackQuery.Data is "1" && !botClient.GetChatAdministratorsAsync(update.CallbackQuery.Message.Chat.Id).Result.Any((chatMember) => chatMember.User.Id == update.CallbackQuery.From.Id))
            || !data.ContainsKey(update.CallbackQuery.From.Id)
            || !data[update.CallbackQuery.From.Id].ContainsKey(update.CallbackQuery.Message.Chat.Id)
            || data[update.CallbackQuery.From.Id][update.CallbackQuery.Message.Chat.Id] != update.CallbackQuery.Message.MessageId
           )
        {
            _ = botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "验证失败");
            return;
        }
        _ = botClient.RestrictChatMemberAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.From.Id, update.Message.Chat.Permissions, DateTime.UtcNow);
        _ = botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "验证成功");
        _ = botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
        _ = data[update.CallbackQuery.From.Id].Remove(update.CallbackQuery.Message.Chat.Id);
        return;
    }
    if (
        !botClient.GetChatAdministratorsAsync(update.Message.Chat.Id).Result.Any((chatMember) => chatMember.User.Id == botClient.BotId)
        || update.Type is not UpdateType.Message
        || update.Message.Type is not MessageType.ChatMembersAdded
       )
    {
        return;
    }
    foreach (User member in update.Message.NewChatMembers)
    {
        if (member.IsBot)
        {
            continue;
        }
        if (!data.ContainsKey(member.Id))
        {
            data[member.Id] = new();
        }
        _ = botClient.RestrictChatMemberAsync(update.Message.Chat.Id, member.Id, new ChatPermissions()
        {
            CanSendMessages = false,
            CanSendMediaMessages = false,
            CanSendPolls = false,
            CanSendOtherMessages = false,
            CanAddWebPagePreviews = false,
            CanChangeInfo = false,
            CanInviteUsers = false,
            CanPinMessages = false,
            CanManageTopics = false
        });
        Message msg = botClient.SendTextMessageAsync(update.Message.Chat.Id, $"你好，@{member.Username}！您已申请加入本群组\n请点击下方按钮进行人机验证，本次验证将于三分钟后失效。", messageThreadId: (update.Message.Chat.IsForum ?? false) ? 114 : default, replyMarkup: new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("验证", "0"),
            InlineKeyboardButton.WithCallbackData("人工通过", "1")
        })).Result;
        data[member.Id][update.Message.Chat.Id] = msg.MessageId;
        Timer timer = new()
        {
            AutoReset = false,
            Interval = 300000
        };
        timer.Elapsed += (_, _) =>
        {
            _ = botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
            _ = botClient.BanChatMemberAsync(update.Message.Chat.Id, member.Id);
            _ = botClient.UnbanChatMemberAsync(update.Message.Chat.Id, member.Id);
            _ = data[member.Id].Remove(update.Message.Chat.Id);
        };
        timer.Start();
    }
}, default);
while (true)
{
    _ = Console.ReadLine();
}
