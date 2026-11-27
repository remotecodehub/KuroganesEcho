using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace KuroganesEcho.Core.Localization;

/// <summary>
/// Manages localization settings for the game, discovering supported cultures directly
/// from the compiled localization resources and applying validated cultures safely.
/// </summary>
internal class LocalizationManager
{
    /// <summary>
    /// The culture code we default to when no valid user preference exists.
    /// </summary>
    public const string DEFAULT_CULTURE_CODE = "pt-BR";

    private static readonly string[] PreferredOrder =
    {
        "pt-BR",
        "en-US",
        "fr-FR",
        "es-ES"
    };

    private static readonly Lazy<IReadOnlyList<CultureInfo>> CachedSupportedCultures =
        new(DiscoverSupportedCultures, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Retrieves every culture backed by available localization resources.
    /// </summary>
    public static List<CultureInfo> GetSupportedCultures() =>
        new(CachedSupportedCultures.Value);

    /// <summary>
    /// Checks whether the specified culture code corresponds to an available localization resource.
    /// </summary>
    public static bool IsSupportedCulture(string cultureCode) =>
        TryResolveSupportedCulture(cultureCode, out _);

    /// <summary>
    /// Attempts to resolve the specified culture code to one of the supported cultures.
    /// </summary>
    public static bool TryResolveSupportedCulture(string cultureCode, out CultureInfo culture)
    {
        culture = null;
        if (string.IsNullOrWhiteSpace(cultureCode))
            return false;

        var trimmed = cultureCode.Trim();
        foreach (var supported in CachedSupportedCultures.Value)
        {
            if (string.Equals(supported.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                culture = supported;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves a culture code to a canonical supported code, falling back to <see cref="DEFAULT_CULTURE_CODE"/>.
    /// </summary>
    public static string ResolveOrDefaultCultureCode(string cultureCode)
    {
        if (TryResolveSupportedCulture(cultureCode, out var supported))
            return supported.Name;

        if (TryResolveSupportedCulture(DEFAULT_CULTURE_CODE, out var fallback))
            return fallback.Name;

        return CachedSupportedCultures.Value.Count > 0
            ? CachedSupportedCultures.Value[0].Name
            : DEFAULT_CULTURE_CODE;
    }

    /// <summary>
    /// Applies the specified culture if and only if it is supported by the localization resources.
    /// Rejects null, invalid, or unsupported culture codes safely without mutating the active culture.
    /// </summary>
    public static bool TrySetCulture(string cultureCode)
    {
        if (!TryResolveSupportedCulture(cultureCode, out var culture))
            return false;

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        return true;
    }

    /// <summary>
    /// Sets the current culture when supported; safely rejects unsupported culture codes.
    /// </summary>
    public static bool SetCulture(string cultureCode) => TrySetCulture(cultureCode);

    /// <summary>
    /// Returns a localized display name for the given culture, falling back to its native name
    /// so newly added localization resources display cleanly without hardcoded switches.
    /// </summary>
    public static string GetDisplayLabel(CultureInfo culture)
    {
        if (culture is null)
            return string.Empty;

        var key = culture.Name switch
        {
            "pt-BR" => "Portuguese",
            "en-US" => "English",
            "fr-FR" => "French",
            "es-ES" => "Spanish",
            _ => $"Language_{culture.Name}"
        };

        var localized = Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        if (!string.IsNullOrWhiteSpace(localized))
            return localized;

        var native = culture.NativeName;
        return string.IsNullOrWhiteSpace(native) ? culture.Name : native;
    }

    private static IReadOnlyList<CultureInfo> DiscoverSupportedCultures()
    {
        var assembly = typeof(Resources).Assembly;
        var candidateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attribute.Key, "SupportedCulture", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(attribute.Value))
            {
                candidateCodes.Add(attribute.Value.Trim());
            }
        }

        // Also scan satellite assembly directories beside the assembly if available on disk.
        var assemblyLocation = assembly.Location;
        var baseDir = string.IsNullOrWhiteSpace(assemblyLocation)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(assemblyLocation);
        var satelliteFileName = $"{assembly.GetName().Name}.resources.dll";

        if (!string.IsNullOrWhiteSpace(baseDir) && Directory.Exists(baseDir))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(baseDir))
                {
                    var folderName = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(folderName))
                        continue;

                    if (File.Exists(Path.Combine(dir, satelliteFileName)))
                        candidateCodes.Add(folderName);
                }
            }
            catch
            {
                // Platforms with sandboxed or virtualized bundles rely on assembly metadata above.
            }
        }

        var supported = new List<CultureInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in candidateCodes)
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(code);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(culture.Name) || !seen.Add(culture.Name))
                continue;

            try
            {
                if (Resources.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false) is not null)
                    supported.Add(culture);
            }
            catch
            {
                // Ignore invalid or unloadable satellite resources.
            }
        }

        supported.Sort(CompareCultures);
        return supported;
    }

    private static int CompareCultures(CultureInfo left, CultureInfo right)
    {
        var leftIndex = Array.FindIndex(PreferredOrder, code => string.Equals(code, left.Name, StringComparison.OrdinalIgnoreCase));
        var rightIndex = Array.FindIndex(PreferredOrder, code => string.Equals(code, right.Name, StringComparison.OrdinalIgnoreCase));

        if (leftIndex >= 0 && rightIndex >= 0)
            return leftIndex.CompareTo(rightIndex);
        if (leftIndex >= 0)
            return -1;
        if (rightIndex >= 0)
            return 1;

        return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
    }
}
