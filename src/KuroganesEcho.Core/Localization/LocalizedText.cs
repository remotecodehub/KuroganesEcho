using System.Globalization;

namespace KuroganesEcho.Core.Localization;

internal static class LocalizedText
{
    public static string Get(string key) =>
        Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
