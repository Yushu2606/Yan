namespace Yanzheng.Type;

internal record struct Config
{
    public string Token { get; set; }
    public string Proxy { get; set; }
    public bool EnableAutoI18n { get; set; }
}
