using System;
using System.Collections.Generic;
using System.Globalization;
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
        "zh-CN",
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
        "en-US",
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
botClient.StartReceiving(async (_, update, _) =>
{
    if (update.Type is UpdateType.CallbackQuery)
    {
        LanguagePack lang = (!string.IsNullOrEmpty(update.CallbackQuery.From.LanguageCode)) ? langPacks.TryGetValue(update.CallbackQuery.From.LanguageCode, out LanguagePack value) ? value : langPacks[CultureInfo.CurrentCulture.Name] : langPacks[CultureInfo.CurrentCulture.Name];
        if ((update.CallbackQuery.Data is "1" && !(await botClient.GetChatAdministratorsAsync(update.CallbackQuery.Message.Chat.Id)).Any((chatMember) => chatMember.User.Id == update.CallbackQuery.From.Id))
            || !data.TryGetValue(update.CallbackQuery.From.Id, out Dictionary<long, int> value1)
            || !value1.ContainsKey(update.CallbackQuery.Message.Chat.Id)
            || value1[update.CallbackQuery.Message.Chat.Id] != update.CallbackQuery.Message.MessageId)
        {
            await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang.Failed);
            return;
        }
        await botClient.RestrictChatMemberAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.From.Id, new()
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
        await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang.Pass);
        await botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
        _ = value1.Remove(update.CallbackQuery.Message.Chat.Id);
        return;
    }
    if (update.Type is not UpdateType.Message)
    {
        return;
    }
    if (update.Message.Type is MessageType.ChatMemberLeft
        && data.TryGetValue(update.Message.From.Id, out Dictionary<long, int> value2)
        && value2.ContainsKey(update.Message.Chat.Id))
    {
        _ = value2.Remove(update.Message.Chat.Id);
    }
    if (update.Message.Type is not MessageType.ChatMembersAdded)
    {
        return;
    }
    foreach (User member in update.Message.NewChatMembers)
    {
        LanguagePack lang = (!string.IsNullOrEmpty(member.LanguageCode)) ? langPacks.TryGetValue(member.LanguageCode, out LanguagePack value3) ? value3 : langPacks[CultureInfo.CurrentCulture.Name] : langPacks[CultureInfo.CurrentCulture.Name];
        if (member.IsBot)
        {
            continue;
        }
        if (!data.ContainsKey(member.Id))
        {
            data[member.Id] = new();
        }
        await botClient.RestrictChatMemberAsync(update.Message.Chat.Id, member.Id, new ChatPermissions()
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
        Message msg = await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang.Message.Replace("%1", member.Username).Replace("%2", min.ToString()), messageThreadId: (update.Message.Chat.IsForum ?? false) ? 1 : default, replyMarkup: new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData(lang.VerifyButton, 0.ToString()),
            InlineKeyboardButton.WithCallbackData(lang.ManualButton, 1.ToString())
        }));
        data[member.Id][update.Message.Chat.Id] = msg.MessageId;
        Timer timer = new()
        {
            AutoReset = false,
            Interval = min * 60000
        };
        timer.Elapsed += async (_, _) =>
        {
            if (!data.TryGetValue(member.Id, out Dictionary<long, int> chats) || !chats.ContainsKey(update.Message.Chat.Id))
            {
                return;
            }
            await botClient.DeleteMessageAsync(update.Message.Chat.Id, msg.MessageId);
            await botClient.BanChatMemberAsync(update.Message.Chat.Id, member.Id);
            await botClient.UnbanChatMemberAsync(update.Message.Chat.Id, member.Id);
            _ = data[member.Id].Remove(update.Message.Chat.Id);
        };
        timer.Start();
    }
}, (_, _, _) => { });
while (true)
{
    _ = Console.ReadLine();
}
