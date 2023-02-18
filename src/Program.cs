using LiteDB;
using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yanzheng.Type;
using Yanzheng.Utils;
using File = System.IO.File;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Timer = System.Timers.Timer;

Config config = new()
{
    Proxy = string.Empty,
    Token = string.Empty,
    EnableAutoI18n = true
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

I18nHelper i18nHelper = new();
foreach (FileInfo file in new DirectoryInfo("languagePack").GetFiles("*.json"))
{
    i18nHelper[Path.GetFileNameWithoutExtension(file.Name)] = new(JsonSerializer.Deserialize<Dictionary<string, string>>(FileHelper.CheckFile(file.FullName, JsonSerializer.Serialize(new Dictionary<string, string>()))));
}

LiteDatabase dataBase = new("data.db");
Dictionary<long, Dictionary<long, int>> data = new();

TelegramBotClient botClient = new(config.Token, string.IsNullOrWhiteSpace(config.Proxy) ? default : new(new HttpClientHandler
{
    Proxy = new WebProxy(config.Proxy, true)
}));
botClient.StartReceiving(async (_, update, _) =>
{
    switch (update.Type)
    {
        case UpdateType.CallbackQuery:
            {
                Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.CallbackQuery.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.CallbackQuery.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                if (!data.TryGetValue(update.CallbackQuery.From.Id, out Dictionary<long, int> value1)
                    || !value1.ContainsKey(update.CallbackQuery.Message.Chat.Id)
                    || value1[update.CallbackQuery.Message.Chat.Id] != update.CallbackQuery.Message.MessageId
                    || (update.CallbackQuery.Data is "1" && !(await botClient.GetChatAdministratorsAsync(update.CallbackQuery.Message.Chat.Id)).Any((chatMember) => chatMember.User.Id == update.CallbackQuery.From.Id)))
                {
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Failed"]);
                    break;
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
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, lang["Pass"]);
                await botClient.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
                value1.Remove(update.CallbackQuery.Message.Chat.Id);
                break;
            }
        case UpdateType.Message:
            {
                switch (update.Message.Type)
                {
                    case MessageType.ChatMemberLeft:
                        {
                            if (data.TryGetValue(update.Message.From.Id, out Dictionary<long, int> value2) && value2.ContainsKey(update.Message.Chat.Id))
                            {
                                value2.Remove(update.Message.Chat.Id);
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
                                Message msg = await botClient.SendTextMessageAsync(update.Message.Chat.Id, lang["Message"].Replace("%1", member.Username).Replace("%2", min.ToString()), messageThreadId: (update.Message.Chat.IsForum ?? false) ? dataBase.GetCollection<ChatData>("chats").FindOne(x => x.ChatId == update.Message.Chat.Id).MessageThreadId : default, replyMarkup: new InlineKeyboardMarkup(new[]
                                {
                                    InlineKeyboardButton.WithCallbackData(lang["VerifyButton"], 0.ToString()),
                                    InlineKeyboardButton.WithCallbackData(lang["ManualButton"], 1.ToString())
                                }));
                                data[member.Id][update.Message.Chat.Id] = msg.MessageId;
                                Timer timer = new(min * 60000)
                                {
                                    AutoReset = false,
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
                                    data[member.Id].Remove(update.Message.Chat.Id);
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
                            Internationalization lang = (config.EnableAutoI18n && !string.IsNullOrEmpty(update.Message.From.LanguageCode)) ? i18nHelper.TryGetLanguageData(update.Message.From.LanguageCode, out Internationalization value) ? value : i18nHelper[CultureInfo.CurrentCulture.Name] : i18nHelper[CultureInfo.CurrentCulture.Name];
                            ILiteCollection<ChatData> col = dataBase.GetCollection<ChatData>("chats");
                            IEnumerable<ChatData> data = col.Find(x => x.ChatId == update.Message.Chat.Id);
                            if (data.Any())
                            {
                                ChatData datum = data.First();
                                datum.MessageThreadId = update.Message.MessageThreadId ?? 1;
                                col.Update(datum);
                                break;
                            }
                            col.Insert(new ChatData()
                            {
                                ChatId = update.Message.Chat.Id,
                                MessageThreadId = update.Message.MessageThreadId ?? 1
                            });
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
