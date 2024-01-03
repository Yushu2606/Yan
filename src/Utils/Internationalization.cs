namespace Yan.Utils;

internal class Internationalization(IReadOnlyDictionary<string, string> langData)
{
    public string this[string languageCode] => Translate(languageCode);

    /// <summary>
    ///     获取翻译
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="values">参数</param>
    /// <returns>翻译完成的信息</returns>
    public string Translate(string key, params object[] values)
    {
        return !langData.TryGetValue(key, out string? value)
            ? key
            : string.Format(value, values);
    }
}