using System.Net;
using LiteDB;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Yan.Utils;
using File = System.IO.File;

namespace Yan;

internal static class Program
{
    public static readonly ConfigHelper Config;
    public static readonly I18nHelper I18n;
    public static readonly LiteDatabase Database;
    public static readonly Dictionary<long, Dictionary<long, int>> GroupData;
    public static readonly TelegramBotClient BotClient;

    static Program()
    {
        Config = new("Config.json");
        I18n = new("LanguagePack");
        Database = new("Groups.db");
        GroupData = new();
        BotClient = new(Config.Token,
            string.IsNullOrWhiteSpace(Config.ProxyUrl)
                ? default
                : new(
                    new HttpClientHandler
                    {
                        Proxy = new WebProxy(Config.ProxyUrl, true)
                    }
                )
        );
    }

    private static void Main()
    {
        BotClient.StartReceiving(async (_, update, _) =>
        {
            try
            {
                switch (update.Type)
                {
                    case UpdateType.CallbackQuery:
                    {
                        if (update.CallbackQuery is null)
                        {
                            break;
                        }

                        await update.CallbackQuery.OnCallback();
                        break;
                    }
                    case UpdateType.Message:
                    {
                        if (update.Message is null)
                        {
                            break;
                        }

                        switch (update.Message.Type)
                        {
                            case MessageType.Text:
                            {
                                if (update.Message.Text != $"/set@{(await BotClient.GetMeAsync()).Username}")
                                {
                                    break;
                                }

                                await update.Message.OnSet();
                            }
                                break;
                            case MessageType.ChatMembersAdded:
                            {
                                if (update.Message.NewChatMembers is null ||
                                    !GroupData.TryGetValue(update.Message.Chat.Id, out Dictionary<long, int>? data))
                                {
                                    break;
                                }

                                foreach (User member in update.Message.NewChatMembers)
                                {
                                    await member.OnJoin(update.Message.Chat.Id, data);
                                }

                                break;
                            }
                            case MessageType.Unknown:
                            case MessageType.Photo:
                            case MessageType.Audio:
                            case MessageType.Video:
                            case MessageType.Voice:
                            case MessageType.Document:
                            case MessageType.Sticker:
                            case MessageType.Location:
                            case MessageType.Contact:
                            case MessageType.Venue:
                            case MessageType.Game:
                            case MessageType.VideoNote:
                            case MessageType.Invoice:
                            case MessageType.SuccessfulPayment:
                            case MessageType.WebsiteConnected:
                            case MessageType.ChatMemberLeft:
                            case MessageType.ChatTitleChanged:
                            case MessageType.ChatPhotoChanged:
                            case MessageType.MessagePinned:
                            case MessageType.ChatPhotoDeleted:
                            case MessageType.GroupCreated:
                            case MessageType.SupergroupCreated:
                            case MessageType.ChannelCreated:
                            case MessageType.MigratedToSupergroup:
                            case MessageType.MigratedFromGroup:
                            case MessageType.Poll:
                            case MessageType.Dice:
                            case MessageType.MessageAutoDeleteTimerChanged:
                            case MessageType.ProximityAlertTriggered:
                            case MessageType.WebAppData:
                            case MessageType.VideoChatScheduled:
                            case MessageType.VideoChatStarted:
                            case MessageType.VideoChatEnded:
                            case MessageType.VideoChatParticipantsInvited:
                            case MessageType.Animation:
                            case MessageType.ForumTopicCreated:
                            case MessageType.ForumTopicClosed:
                            case MessageType.ForumTopicReopened:
                            case MessageType.ForumTopicEdited:
                            case MessageType.GeneralForumTopicHidden:
                            case MessageType.GeneralForumTopicUnhidden:
                            case MessageType.WriteAccessAllowed:
                            case MessageType.UserShared:
                            case MessageType.ChatShared:
                            case MessageType.Story:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    }
                    case UpdateType.ChatJoinRequest:
                    {
                        if (update.ChatJoinRequest is null || update.ChatJoinRequest.From.IsBot)
                        {
                            break;
                        }

                        await update.ChatJoinRequest.OnRequest();
                        break;
                    }
                    case UpdateType.Unknown:
                    case UpdateType.InlineQuery:
                    case UpdateType.ChosenInlineResult:
                    case UpdateType.EditedMessage:
                    case UpdateType.ChannelPost:
                    case UpdateType.EditedChannelPost:
                    case UpdateType.ShippingQuery:
                    case UpdateType.PreCheckoutQuery:
                    case UpdateType.Poll:
                    case UpdateType.PollAnswer:
                    case UpdateType.MyChatMember:
                    case UpdateType.ChatMember:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("Exception.log", $"[{DateTime.Now}] {ex}\n");
            }
        }, (_, e, _) => { });

        while (true)
        {
            Console.ReadLine();
        }
    }
}