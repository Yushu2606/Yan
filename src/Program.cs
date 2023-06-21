using LiteDB;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yanzheng.Utils;
using Timer = System.Timers.Timer;

ConfigHelper config = new("config.json");

I18nHelper i18nHelper = new("languagePack");

LiteDatabase dataBase = new("data.db");
Dictionary<long, Dictionary<int, long>> groupData = new();

TelegramBotClient botClient = new(config.Token, string.IsNullOrWhiteSpace(config.ProxyUrl) ? default : new(new HttpClientHandler
{
    Proxy = new WebProxy(config.ProxyUrl, true)
}));
botClient.StartReceiving(async (_, update, _) =>
{
    try
    {
        switch (update.Type)
        {
            case UpdateType.CallbackQuery:
                {
                    Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.CallbackQuery.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.CallbackQuery.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                    if (!groupData.TryGetValue(update.CallbackQuery.Message.Chat.Id, out Dictionary<int, long> data)
                         || !data.TryGetValue(update.CallbackQuery.Message.MessageId, out long userId)
                         || userId != update.CallbackQuery.From.Id)
                    {
                        await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Failed"]);
                        break;
                    }
                    try
                    {
                        await botClient.ApproveChatJoinRequest(update.CallbackQuery.Message.Chat.Id, data[update.CallbackQuery.Message.MessageId]);
                    }
                    catch (ApiRequestException)
                    {
                        await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Failed"]);
                        break;
                    }
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Pass"]);
                    break;
                }
            case UpdateType.Message:
                {
                    switch (update.Message.Type)
                    {
                        case MessageType.Text:
                            {
                                Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.Message.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.Message.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                                if (update.Message.Text != $"/set@{(await botClient.GetMeAsync()).Username}")
                                {
                                    break;
                                }
                                if (!(update.Message.Chat.IsForum ?? false) || !(await botClient.GetChatAdministratorsAsync(update.Message.Chat.Id)).Any((chatMember) => chatMember.User.Id == update.Message.From.Id))
                                {
                                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang["UpdateFailed"], (update.Message.Chat.IsForum ?? false) ? update.Message.MessageThreadId : default, replyToMessageId: update.Message.MessageId);
                                    break;
                                }
                                update.Message.Chat.JoinByRequest = true;
                                ILiteCollection<ChatData> col = dataBase.GetCollection<ChatData>("chats");
                                col.Upsert(new ChatData(update.Message.Chat.Id, update.Message.MessageThreadId ?? default));
                                await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang["UpdateSuccess"], update.Message.MessageThreadId, replyToMessageId: update.Message.MessageId);
                            }
                            break;
                        case MessageType.ChatMembersAdded:
                            {
                                foreach (User member in update.Message.NewChatMembers)
                                {
                                    if (!groupData.TryGetValue(update.Message.Chat.Id, out Dictionary<int, long> data))
                                    {
                                        break;
                                    }
                                    foreach ((int messageId, long userId) in data)
                                    {
                                        if (userId != member.Id)
                                        {
                                            continue;
                                        }
                                        await botClient.DeleteMessageAsync(update.Message.Chat.Id, messageId);
                                        data.Remove(messageId);
                                    }
                                }
                                break;
                            }
                    }
                    break;
                }
            case UpdateType.ChatJoinRequest:
                {
                    Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.ChatJoinRequest.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.ChatJoinRequest.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                    if (update.ChatJoinRequest.From.IsBot)
                    {
                        break;
                    }
                    if (!groupData.ContainsKey(update.ChatJoinRequest.Chat.Id))
                    {
                        groupData[update.ChatJoinRequest.Chat.Id] = new();
                    }
                    int min = 3;    // TODO：群组管理员自定义时长
                    Message msg = await botClient.SendTextMessageAsync(
                        update.ChatJoinRequest.Chat.Id,
                        lang.Translate("Message", string.IsNullOrWhiteSpace(update.ChatJoinRequest.From.Username) ? update.ChatJoinRequest.From.FirstName : update.ChatJoinRequest.From.Username, min),
                        messageThreadId: (update.ChatJoinRequest.Chat.IsForum ?? false) ? dataBase.GetCollection<ChatData>("chats").FindById(update.ChatJoinRequest.Chat.Id).MessageThreadId : default,
                        replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                        InlineKeyboardButton.WithCallbackData(lang["VerifyButton"]),
                        }));
                    groupData[update.ChatJoinRequest.Chat.Id][msg.MessageId] = update.ChatJoinRequest.UserChatId;
                    Timer timer = new(min * 60000)
                    {
                        AutoReset = false,
                    };
                    timer.Elapsed += async (_, _) =>
                    {
                        if (!groupData.TryGetValue(update.ChatJoinRequest.Chat.Id, out Dictionary<int, long> members) || !members.ContainsKey(msg.MessageId))
                        {
                            return;
                        }
                        members.Remove(msg.MessageId);
                        try
                        {

                            await botClient.DeclineChatJoinRequest(update.ChatJoinRequest.Chat.Id, update.ChatJoinRequest.UserChatId);
                        }
                        catch (ApiRequestException)
                        {
                            return;
                        }
                        await botClient.DeleteMessageAsync(update.ChatJoinRequest.Chat.Id, msg.MessageId);
                    };
                    timer.Start();
                    break;
                }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
}, (_, e, _) => { ExceptionDispatchInfo.Capture(e).Throw(); });
while (true)
{
    Console.ReadLine();
}
