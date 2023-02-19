namespace Yanzheng.Utils;

internal class I18nHelper
{
    private readonly Dictionary<string, Internationalization> _language = new();
    internal bool TryGetLanguageData(string languageCode, out Internationalization languageData) => _language.TryGetValue(languageCode, out languageData);
    internal void AddLanguage(string languageCode, Internationalization languageData)
    {
        if (_language.ContainsKey(languageCode))
        {
            throw new InvalidDataException($"{languageCode} already exist, please check your languange file");
        }
        _language[languageCode] = languageData;
    }
    internal Internationalization this[string languageCode]
    {
        get => TryGetLanguageData(languageCode, out Internationalization languageData)
                ? languageData
                : throw new KeyNotFoundException($"{languageCode} not found, please check your language file");
        set => AddLanguage(languageCode, value);
    }
}
