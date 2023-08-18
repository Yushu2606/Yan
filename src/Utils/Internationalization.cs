namespace Yan.Utils;

internal class Internationalization
{
    private readonly Dictionary<string, string> _languageData;
    private readonly string _name;
    public Internationalization(Dictionary<string, string> langData, string name = "")
    {
        _languageData = langData;
        _name = name;
    }
    /// <summary>
    /// 获取翻译
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="values">参数</param>
    /// <returns>翻译完成的信息</returns>
    public string Translate(string key, params object[] values) => !_languageData.TryGetValue(key, out string? value)
            ? key
            : string.Format(value, values);
    public string this[string languageCode] => Translate(languageCode);
}
