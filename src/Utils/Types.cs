namespace Yanzheng.Utils;

internal record struct ChatData(long ChatId, int MessageThreadId);
internal record struct Config(string Token, string Proxy, bool EnableAutoI18n);
