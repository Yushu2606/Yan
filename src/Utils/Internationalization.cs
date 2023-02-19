namespace Yanzheng.Utils;

internal class Internationalization
{
    private readonly Dictionary<string, string> _languageData;
    private readonly string _name;
    internal Internationalization(Dictionary<string, string> langData, string name = "")
    {
        _languageData = langData;
        _name = name;
    }
    /// <summary>
    /// 获取翻译
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="kvs">参数</param>
    /// <returns>翻译完成的信息</returns>
    internal string Translate(string key, Dictionary<string, string> kvs = default)
    {
        if (!_languageData.TryGetValue(key, out string value))
        {
            throw new KeyNotFoundException($"{key} not find{(string.IsNullOrWhiteSpace(_name) ? string.Empty : $" in ${_name}")}, please check your language file");
        }
        if (kvs is null)
        {
            return value;
        }
        foreach ((string k, string v) in kvs)
        {
            value = value.Replace($"%{k}%", v);
        }
        return value;
    }
    internal string this[string languageCode] => Translate(languageCode);
}
