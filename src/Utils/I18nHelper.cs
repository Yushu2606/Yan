using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Yanzheng.Utils;

internal class I18nHelper
{
    public I18nHelper(string path)
    {
        DirectoryInfo langFileDir = FileHelper.CheckDir(path);
        string defaultValue = JsonSerializer.Serialize(new Dictionary<string, string>());
        foreach (FileInfo file in langFileDir.GetFiles("*.json"))
        {
            Dictionary<string, string>? kv = JsonSerializer.Deserialize<Dictionary<string, string>>(FileHelper.CheckFile(file.FullName, defaultValue));
            if (kv is null)
            {
                continue;
            }
            this[Path.GetFileNameWithoutExtension(file.Name)] = new(kv);
        }
    }
    private readonly Dictionary<string, Internationalization> _language = new();
    public bool TryGetLanguageData(string languageCode, [NotNullWhen(true)] out Internationalization? languageData) => _language.TryGetValue(languageCode, out languageData);
    public void AddLanguage(string languageCode, Internationalization languageData) => _language[languageCode] = languageData;
    public Internationalization this[string languageCode]
    {
        get
        {
            if (TryGetLanguageData(languageCode, out Internationalization? languageData))
            {
                return languageData;
            }
            Internationalization data = new(new(), languageCode);
            _language[languageCode] = data;
            return data;
        }
        set => AddLanguage(languageCode, value);
    }
    public Internationalization GetI18n(string? languageCode)
    {
        return (Program.Config.EnableAutoI18n && !string.IsNullOrEmpty(languageCode)) ? TryGetLanguageData(languageCode, out Internationalization? value) ? value : this[CultureInfo.CurrentCulture.Name] : this[CultureInfo.CurrentCulture.Name];
    }
}
