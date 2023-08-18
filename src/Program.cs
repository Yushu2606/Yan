using LiteDB;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Yan;
using Yan.Utils;

internal static class Program
{
    public static ConfigHelper Config;
    public static I18nHelper I18n;
    public static LiteDatabase Database;
    public static Dictionary<long, Dictionary<long, int>> GroupData;
    public static TelegramBotClient BotClient;

    static Program()
    {
        Config = new("Config.json");
        I18n = new("LanguagePack");
        Database = new("Groups.db");
        GroupData = new();
        BotClient = new(Config.Token,
            string.IsNullOrWhiteSpace(Config.ProxyUrl) ? default : new(
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
                switch (update.Type)
                {
                    case UpdateType.CallbackQuery:
                        {
                            if (update.CallbackQuery is null)
                            {
                                break;
                            }
                            update.CallbackQuery.OnCallback();
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
                                        update.Message.OnSet();
                                    }
                                    break;
                                case MessageType.ChatMembersAdded:
                                    {
                                        if (update.Message.NewChatMembers is null || !GroupData.TryGetValue(update.Message.Chat.Id, out Dictionary<long, int>? data))
                                        {
                                            break;
                                        }
                                        foreach (User member in update.Message.NewChatMembers)
                                        {
                                            member.OnJoin(update.Message.Chat.Id, data);
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case UpdateType.ChatJoinRequest:
                        {
                            if (update.ChatJoinRequest is null || update.ChatJoinRequest.From.IsBot)
                            {
                                break;
                            }
                            update.ChatJoinRequest.OnRequest();
                            break;
                        }
                }
        }, (_, e, _) => { });

        while (true)
        {
            Console.ReadLine();
        }
    }
}