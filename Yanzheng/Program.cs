using System;
using System.Collections.Generic;
using System.IO;
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

Dictionary<string, LanguagePack> langPacks = new()
{
    {
        "zh-hans",
        new()
        {
            Message = "你好，@%1！您已申请加入本群组\n请点击下方按钮进行人机验证，本次验证将于%2分钟后失效。",
            VerifyButton = "验证",
            ManualButton = "人工通过",
            Pass = "验证成功",
            Failed = "验证失败"
        }
    },
    {
        "en",
        new()
        {
            Message = "Hello, @%1! You have applied to join this group.\nPlease click the button below for man-machine verification. This verification will expire in %2 minutes.",
            VerifyButton = "Captcha",
            ManualButton = "Manual Pass",
            Pass = "Verify Passed",
            Failed = "Verify Failed"
        }
    }
};
if (!Directory.Exists("lang"))
{
    _ = Directory.CreateDirectory("lang");
    foreach ((string langCode, LanguagePack langPack) in langPacks)
    {
        File.WriteAllText(Path.Combine("lang", $"{langCode}.json"), JsonSerializer.Serialize(langPack, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }));
    }
}
foreach (string file in Directory.GetFiles("lang", "*.json"))
{
    langPacks[Path.GetFileNameWithoutExtension(file)] = JsonSerializer.Deserialize<LanguagePack>(File.ReadAllText(file));
}

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
        LanguagePack lang = langPacks.TryGetValue(update.CallbackQuery.From.LanguageCode, out LanguagePack value) ? value : langPacks["en"];
        if ((update.CallbackQuery.Data is "1" && !botClient.GetChatAdministratorsAsync(update.CallbackQuery.Message.Chat.Id).Result.Any((chatMember) => chatMember.User.Id == update.CallbackQuery.From.Id))
            || !data.ContainsKey(update.CallbackQuery.From.Id)
            || !data[update.CallbackQuery.From.Id].ContainsKey(update.CallbackQuery.Message.Chat.Id)
            || data[update.CallbackQuery.From.Id][update.CallbackQuery.Message.Chat.Id] != update.CallbackQuery.Message.MessageId)
        {
            _ = botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang.Failed);
            return;
        }
        _ = botClient.RestrictChatMemberAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.From.Id, new()
        {
            CanSendMessages = true,
            CanSendMediaMessages = true,
            CanSendPolls = true,
            CanSendOtherMessages = true,
            CanAddWebPagePreviews = true,
            CanChangeInfo = true,
            CanInviteUsers = true,
            CanPinMessages = true,
            CanManageTopics = true
        }, DateTime.UtcNow);
        _ = botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang.Pass);
        _ = botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
        _ = data[update.CallbackQuery.From.Id].Remove(update.CallbackQuery.Message.Chat.Id);
        return;
    }
    if (update.Type is not UpdateType.Message)
    {
        return;
    }
    if (update.Message.Type is MessageType.ChatMemberLeft
        && data.ContainsKey(update.Message.From.Id)
        && data[update.Message.From.Id].ContainsKey(update.Message.Chat.Id))
    {
        _ = data[update.Message.From.Id].Remove(update.Message.Chat.Id);
    }
    if (update.Message.Type is not MessageType.ChatMembersAdded)
    {
        return;
    }
    foreach (User member in update.Message.NewChatMembers)
    {
        LanguagePack lang = langPacks.TryGetValue(member.LanguageCode, out LanguagePack value) ? value : langPacks["en"];
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
        int min = 3;
        Message msg = botClient.SendTextMessageAsync(update.Message.Chat.Id, lang.Message.Replace("%1", member.Username).Replace("%2", min.ToString()), messageThreadId: (update.Message.Chat.IsForum ?? false) ? 114 : default, replyMarkup: new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData(lang.VerifyButton, 0.ToString()),
            InlineKeyboardButton.WithCallbackData(lang.ManualButton, 1.ToString())
        })).Result;
        data[member.Id][update.Message.Chat.Id] = msg.MessageId;
        Timer timer = new()
        {
            AutoReset = false,
            Interval = min * 60000
        };
        timer.Elapsed += (_, _) =>
        {
            _ = botClient.DeleteMessageAsync(update.Message.Chat.Id, msg.MessageId);
            _ = botClient.BanChatMemberAsync(update.Message.Chat.Id, member.Id);
            _ = botClient.UnbanChatMemberAsync(update.Message.Chat.Id, member.Id);
            _ = data[member.Id].Remove(update.Message.Chat.Id);
        };
        timer.Start();
    }
}, (_, _, _) => { });
while (true)
{
    _ = Console.ReadLine();
}
