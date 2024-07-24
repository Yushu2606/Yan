using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Yan.Utils;
using File = System.IO.File;

namespace Yan;

internal static class Program
{
    public static readonly Config Config;
    public static readonly L10nProvider Localizer;
    public static readonly LiteDatabase Database;
    public static readonly Dictionary<long, Dictionary<long, int>> GroupData;
    public static readonly TelegramBotClient BotClient;

    static Program()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        IHostEnvironment env = builder.Environment;
        builder.Configuration.AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);
        Config = builder.Configuration.Get<Config>()!;
        Localizer = new("Resources");
        Database = new("Groups.db");
        GroupData = new();
        BotClient = new(Config.Token,
            string.IsNullOrWhiteSpace(Config.ProxyUrl)
                ? default
                : new(new HttpClientHandler
                {
                    Proxy = new WebProxy(Config.ProxyUrl, true, default,
                        string.IsNullOrWhiteSpace(Config.ProxyUserName)
                            ? default
                            : new NetworkCredential(Config.ProxyUserName, Config.ProxyPassword))
                }));
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
                                        break;
                                    }
                                case MessageType.ChatMembersAdded:
                                    {
                                        if (update.Message.NewChatMembers is null ||
                                            !GroupData.TryGetValue(update.Message.Chat.Id,
                                                out Dictionary<long, int>? data))
                                        {
                                            break;
                                        }

                                        foreach (User member in update.Message.NewChatMembers)
                                        {
                                            await member.OnJoin(update.Message.Chat.Id, data);
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

                            await update.ChatJoinRequest.OnRequest();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("logs");
                string logFilePath = $"logs/{DateTime.Now:yy-MM-ddTHH-mm-ss}.log";
                if (File.Exists(logFilePath))
                {
                    await File.AppendAllTextAsync(logFilePath, "\n");
                }

                await File.AppendAllTextAsync(logFilePath, ex.ToString());
            }
        }, (_, e, _) => { });
        Thread.CurrentThread.Join();
    }
}