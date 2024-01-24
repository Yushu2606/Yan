using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Yan.Utils;

internal class L10nProvider
{
    private readonly Dictionary<string, Localizer> _language = new();

    public L10nProvider(string path)
    {
        DirectoryInfo langFileDir = FileHelper.CheckDir(path);
        string defaultValue = JsonSerializer.Serialize(new Dictionary<string, string>());
        foreach (FileInfo file in langFileDir.GetFiles("*.json"))
        {
            Dictionary<string, string>? kv =
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    FileHelper.CheckFile(file.FullName, defaultValue));
            if (kv is null)
            {
                continue;
            }

            this[Path.GetFileNameWithoutExtension(file.Name)] = new(kv);
        }
    }

    public Localizer this[string languageCode]
    {
        get
        {
            if (TryGetLanguageData(languageCode, out Localizer? languageData))
            {
                return languageData;
            }

            Localizer data = new(new Dictionary<string, string>());
            _language[languageCode] = data;
            return data;
        }
        init => AddLanguage(languageCode, value);
    }

    public bool TryGetLanguageData(string languageCode, [NotNullWhen(true)] out Localizer? languageData)
    {
        return _language.TryGetValue(languageCode, out languageData);
    }

    public void AddLanguage(string languageCode, Localizer languageData)
    {
        _language[languageCode] = languageData;
    }

    public Localizer GetLocalizer(string? languageCode)
    {
        return Program.Config.EnableAutoL10n && !string.IsNullOrEmpty(languageCode)
            ? TryGetLanguageData(languageCode, out Localizer? value) ? value : this[CultureInfo.CurrentCulture.Name]
            : this[CultureInfo.CurrentCulture.Name];
    }
}