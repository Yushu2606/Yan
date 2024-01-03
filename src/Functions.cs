using LiteDB;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yan.Utils;
using Timer = System.Timers.Timer;

namespace Yan;

internal static class Functions
{
    public static async Task OnCallback(this CallbackQuery callbackQuery)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        Internationalization lang = Program.I18n.GetI18n(callbackQuery.From.LanguageCode);
        if (!Program.GroupData.TryGetValue(callbackQuery.Message.Chat.Id, out Dictionary<long, int>? data) ||
            !data.TryGetValue(callbackQuery.From.Id, out int historyMessageId) ||
            historyMessageId != callbackQuery.Message.MessageId)
        {
            await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Failed"]);
            return;
        }

        try
        {
            await Program.BotClient.ApproveChatJoinRequest(callbackQuery.Message.Chat.Id, callbackQuery.From.Id);
        }
        catch (ApiRequestException)
        {
            await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Failed"]);
            return;
        }

        await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Pass"]);
    }

    public static async Task OnRequest(this ChatJoinRequest chatJoinRequest)
    {
        if (!Program.GroupData.TryGetValue(chatJoinRequest.Chat.Id, out Dictionary<long, int>? value))
        {
            Program.GroupData[chatJoinRequest.Chat.Id] = new();
        }
        else if (value.ContainsKey(chatJoinRequest.From.Id))
        {
            return;
        }

        Internationalization lang = Program.I18n.GetI18n(chatJoinRequest.From.LanguageCode);
        const int min = 3; // TODO：群组管理员自定义时长
        Message msg = await Program.BotClient.SendTextMessageAsync(
            chatJoinRequest.Chat.Id,
            lang.Translate("Message",
                $"[{(string.IsNullOrWhiteSpace(chatJoinRequest.From.Username) ? $"{chatJoinRequest.From.FirstName} {chatJoinRequest.From.LastName}".Escape() : chatJoinRequest.From.Username)}](tg://user?id={chatJoinRequest.From.Id})",
                min),
            chatJoinRequest.Chat.IsForum ?? false
                ? Program.Database.GetCollection<ChatData>("chats").FindById(chatJoinRequest.Chat.Id).MessageThreadId
                : default,
            ParseMode.MarkdownV2,
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData(lang["VerifyButton"])
            })
        );
        Program.GroupData[chatJoinRequest.Chat.Id][chatJoinRequest.From.Id] = msg.MessageId;
        Timer timer = new(min * 60000)
        {
            AutoReset = false
        };
        timer.Elapsed += async (_, _) =>
        {
            if (!Program.GroupData.TryGetValue(chatJoinRequest.Chat.Id, out Dictionary<long, int>? members) ||
                !members.ContainsKey(chatJoinRequest.From.Id))
            {
                return;
            }

            members.Remove(chatJoinRequest.From.Id);
            try
            {
                await Program.BotClient.DeleteMessageAsync(chatJoinRequest.Chat.Id, msg.MessageId);
            }
            catch (ApiRequestException)
            {
            }
        };
        timer.Start();
    }

    public static async Task OnSet(this Message message)
    {
        if (message.From is null)
        {
            return;
        }

        Internationalization lang = Program.I18n.GetI18n(message.From.LanguageCode);
        if ((!message.Chat.IsForum ?? true) ||
            (await Program.BotClient.GetChatAdministratorsAsync(message.Chat.Id)).All(chatMember =>
                chatMember.User.Id != message.From.Id))
        {
            await Program.BotClient.SendTextMessageAsync(message.Chat.Id, lang["UpdateFailed"],
                message.Chat.IsForum ?? false ? message.MessageThreadId : default, ParseMode.MarkdownV2,
                replyToMessageId: message.MessageId);
            return;
        }

        ILiteCollection<ChatData> col = Program.Database.GetCollection<ChatData>("chats");
        col.Upsert(new ChatData(message.Chat.Id, message.MessageThreadId ?? default));
        await Program.BotClient.SendTextMessageAsync(message.Chat.Id, lang["UpdateSuccess"], message.MessageThreadId,
            ParseMode.MarkdownV2, replyToMessageId: message.MessageId);
    }

    public static async Task OnJoin(this User member, long chatId, Dictionary<long, int> data)
    {
        if (!data.TryGetValue(member.Id, out int value))
        {
            return;
        }

        try
        {
            await Program.BotClient.DeleteMessageAsync(chatId, value);
        }
        catch (ApiRequestException)
        {
        }

        data.Remove(member.Id);
    }
}