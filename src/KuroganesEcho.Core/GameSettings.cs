using System;
using System.IO;
using System.Text.Json;
using KuroganesEcho.Core.Localization;

namespace KuroganesEcho.Core;

/// <summary>
/// Global user preferences persisted independently from gameplay progress and save slots.
/// </summary>
internal sealed class GameSettings
{
    public const float DefaultBgmVolume = 0.65f;
    public const float DefaultSfxVolume = 0.75f;

    public float BgmVolume { get; set; } = DefaultBgmVolume;
    public float SfxVolume { get; set; } = DefaultSfxVolume;
    public bool IsMuted { get; set; }
    public string Language { get; set; } = LocalizationManager.DEFAULT_CULTURE_CODE;
    public bool FullScreen { get; set; } = true;

    public static GameSettings CreateDefault(bool defaultFullScreen = true) => new()
    {
        BgmVolume = DefaultBgmVolume,
        SfxVolume = DefaultSfxVolume,
        IsMuted = false,
        Language = LocalizationManager.ResolveOrDefaultCultureCode(LocalizationManager.DEFAULT_CULTURE_CODE),
        FullScreen = defaultFullScreen
    };

    public GameSettings Clone() => new()
    {
        BgmVolume = BgmVolume,
        SfxVolume = SfxVolume,
        IsMuted = IsMuted,
        Language = Language,
        FullScreen = FullScreen
    };

    public void Sanitize()
    {
        BgmVolume = SanitizeVolume(BgmVolume, DefaultBgmVolume);
        SfxVolume = SanitizeVolume(SfxVolume, DefaultSfxVolume);
        Language = LocalizationManager.ResolveOrDefaultCultureCode(Language);
    }

    internal static float SanitizeVolume(float? value, float fallback)
    {
        if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
            return fallback;

        return Math.Clamp(value.Value, 0f, 1f);
    }
}

/// <summary>
/// Persists global <see cref="GameSettings"/> to a dedicated settings file independent
/// from individual save-slot files.
/// </summary>
internal static class GameSettingsStore
{
    private sealed class GameSettingsDto
    {
        public float? BgmVolume { get; set; }
        public float? SfxVolume { get; set; }
        public bool? IsMuted { get; set; }
        public string Language { get; set; }
        public bool? FullScreen { get; set; }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string FilePath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();
            return Path.Combine(root, "KuroganesEcho", "settings.json");
        }
    }

    public static GameSettings Load(bool defaultFullScreen = true) =>
        Load(FilePath, defaultFullScreen);

    public static GameSettings Load(string filePath, bool defaultFullScreen = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return GameSettings.CreateDefault(defaultFullScreen);

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return GameSettings.CreateDefault(defaultFullScreen);

            var dto = JsonSerializer.Deserialize<GameSettingsDto>(json, SerializerOptions);
            if (dto is null)
                return GameSettings.CreateDefault(defaultFullScreen);

            return new GameSettings
            {
                BgmVolume = GameSettings.SanitizeVolume(dto.BgmVolume, GameSettings.DefaultBgmVolume),
                SfxVolume = GameSettings.SanitizeVolume(dto.SfxVolume, GameSettings.DefaultSfxVolume),
                IsMuted = dto.IsMuted ?? false,
                Language = LocalizationManager.ResolveOrDefaultCultureCode(dto.Language),
                FullScreen = dto.FullScreen ?? defaultFullScreen
            };
        }
        catch
        {
            return GameSettings.CreateDefault(defaultFullScreen);
        }
    }

    public static void Save(GameSettings settings) =>
        Save(FilePath, settings);

    public static void Save(string filePath, GameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(filePath) || settings is null)
            return;

        try
        {
            var sanitized = settings.Clone();
            sanitized.Sanitize();

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(sanitized, SerializerOptions);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // A read-only or unavailable profile directory must not crash the game.
        }
    }
}
