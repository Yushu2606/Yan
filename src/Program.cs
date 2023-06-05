using LiteDB;
using System.Globalization;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yanzheng.Utils;
using Timer = System.Timers.Timer;

ConfigHelper config = new("config.json");

I18nHelper i18nHelper = new("languagePack");

LiteDatabase dataBase = new("data.db");
Dictionary<long, Dictionary<int, long>> data = new();

TelegramBotClient botClient = new(config.Token, string.IsNullOrWhiteSpace(config.ProxyUrl) ? default : new(new HttpClientHandler
{
    Proxy = new WebProxy(config.ProxyUrl, true)
}));
botClient.StartReceiving(async (_, update, _) =>
{
    switch (update.Type)
    {
        case UpdateType.CallbackQuery:
            {
                Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.CallbackQuery.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.CallbackQuery.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                if ((!data.TryGetValue(update.CallbackQuery.Message.Chat.Id, out Dictionary<int, long> value1)
                     || !value1.ContainsKey(update.CallbackQuery.Message.MessageId)
                     || value1[update.CallbackQuery.Message.MessageId] != update.CallbackQuery.From.Id)
                     && !(await botClient.GetChatAdministratorsAsync(update.CallbackQuery.Message.Chat.Id)).Any((chatMember) => chatMember.User.Id == update.CallbackQuery.From.Id))
                {
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Failed"]);
                    break;
                }
                await botClient.ApproveChatJoinRequest(update.CallbackQuery.Message.Chat.Id, value1[update.CallbackQuery.Message.MessageId]);
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Pass"]);
                await botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
                value1.Remove(update.CallbackQuery.Message.MessageId);
                break;
            }
        case UpdateType.Message:
            {
                switch (update.Message.Type)
                {
                    case MessageType.ChatMemberLeft:
                        {
                            if (!data.TryGetValue(update.Message.Chat.Id, out Dictionary<int, long> value2))
                            {
                                break;
                            }
                            foreach ((int message, long member) in value2)
                            {
                                if (member != update.Message.From.Id)
                                {
                                    continue;
                                }
                                await botClient.DeleteMessageAsync(update.Message.Chat.Id, message);
                                value2.Remove(message);
                            }
                            break;
                        }
                    case MessageType.ChatMembersAdded:
                        {
                            foreach (User member in update.Message.NewChatMembers)
                            {
                                Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(member.LanguageCode)) ? i18nHelper.TryGetLanguageData(member.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                                if (member.IsBot)
                                {
                                    continue;
                                }
                                if (!data.ContainsKey(update.Message.Chat.Id))
                                {
                                    data[update.Message.Chat.Id] = new();
                                }
                                int min = 3;    // TODO：群组管理员自定义时长
                                Message msg = await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang.Translate("Message", string.IsNullOrWhiteSpace(member.Username) ? member.FirstName : member.Username, min), messageThreadId: (update.Message.Chat.IsForum ?? false) ? dataBase.GetCollection<ChatData>("chats").FindOne(x => x.ChatId == update.Message.Chat.Id).MessageThreadId : default, replyMarkup: new InlineKeyboardMarkup(new[]
                                {
                                    InlineKeyboardButton.WithCallbackData(lang["VerifyButton"]),
                                }));
                                data[update.Message.Chat.Id][msg.MessageId] = member.Id;
                                Timer timer = new(min * 60000)
                                {
                                    AutoReset = false,
                                };
                                timer.Elapsed += async (_, _) =>
                                {
                                    if (!data.TryGetValue(update.Message.Chat.Id, out Dictionary<int, long> members) || !members.ContainsKey(msg.MessageId))
                                    {
                                        return;
                                    }
                                    await botClient.DeleteMessageAsync(update.Message.Chat.Id, msg.MessageId);
                                    await botClient.BanChatMemberAsync(update.Message.Chat.Id, member.Id);
                                    await botClient.UnbanChatMemberAsync(update.Message.Chat.Id, member.Id);
                                    members.Remove(msg.MessageId);
                                };
                                timer.Start();
                            }
                            break;
                        }
                    case MessageType.Text:
                        {
                            if (update.Message.Text != $"/set@{(await botClient.GetMeAsync()).Username}"
                                || !(update.Message.Chat.IsForum ?? false)
                                || !(await botClient.GetChatAdministratorsAsync(update.Message.Chat.Id)).Any((chatMember) => chatMember.User.Id == update.Message.From.Id))
                            {
                                break;
                            }
                            update.Message.Chat.JoinByRequest = true;
                            Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.Message.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.Message.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                            ILiteCollection<ChatData> col = dataBase.GetCollection<ChatData>("chats");
                            col.DeleteMany(x => x.ChatId == update.Message.Chat.Id);
                            col.Upsert(new ChatData(update.Message.Chat.Id, update.Message.MessageThreadId ?? default));
                            await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang["UpdateSuccess"], update.Message.MessageThreadId, replyToMessageId: update.Message.MessageId);
                            break;
                        }
                }
                break;
            }
    }
}, (_, _, _) => { });
while (true)
{
    Console.ReadLine();
}
