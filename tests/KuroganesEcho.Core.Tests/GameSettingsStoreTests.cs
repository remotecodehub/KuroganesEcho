using System;
using System.IO;
using KuroganesEcho.Core;
using KuroganesEcho.Core.Localization;
using Xunit;

namespace KuroganesEcho.Core.Tests;

public sealed class GameSettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public GameSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"KuroganesEcho_SettingsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup issues in temp directory.
        }
    }

    [Fact]
    public void Load_ReturnsSafeDefaults_WhenSettingsFileDoesNotExist()
    {
        var loaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: true);

        Assert.Equal(0.65f, loaded.BgmVolume, precision: 4);
        Assert.Equal(0.75f, loaded.SfxVolume, precision: 4);
        Assert.False(loaded.IsMuted);
        Assert.Equal(LocalizationManager.DEFAULT_CULTURE_CODE, loaded.Language);
        Assert.True(loaded.FullScreen);
    }

    [Fact]
    public void SaveAndReload_PersistsAllSettingsAcrossRestarts()
    {
        var original = new GameSettings
        {
            BgmVolume = 0.42f,
            SfxVolume = 0.88f,
            IsMuted = true,
            Language = "es-ES",
            FullScreen = false
        };

        GameSettingsStore.Save(_settingsPath, original);
        var reloaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: true);

        Assert.Equal(0.42f, reloaded.BgmVolume, precision: 4);
        Assert.Equal(0.88f, reloaded.SfxVolume, precision: 4);
        Assert.True(reloaded.IsMuted);
        Assert.Equal("es-ES", reloaded.Language);
        Assert.False(reloaded.FullScreen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FullscreenPreferenceSurvivesRestart(bool desiredFullScreen)
    {
        var settings = GameSettings.CreateDefault(defaultFullScreen: !desiredFullScreen);
        settings.FullScreen = desiredFullScreen;

        GameSettingsStore.Save(_settingsPath, settings);
        var reloaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: !desiredFullScreen);

        Assert.Equal(desiredFullScreen, reloaded.FullScreen);
    }

    [Fact]
    public void Load_RecoversGracefullyFromCorruptedJson()
    {
        File.WriteAllText(_settingsPath, "{ this is not valid json !!!");

        var loaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: true);

        Assert.Equal(0.65f, loaded.BgmVolume, precision: 4);
        Assert.Equal(0.75f, loaded.SfxVolume, precision: 4);
        Assert.Equal(LocalizationManager.DEFAULT_CULTURE_CODE, loaded.Language);
        Assert.True(loaded.FullScreen);
    }

    [Fact]
    public void Load_ClampsOutOfRangeVolumeValuesToUnitInterval()
    {
        const string json = """
            {
              "BgmVolume": 2.75,
              "SfxVolume": -0.4,
              "Language": "fr-FR",
              "FullScreen": false
            }
            """;
        File.WriteAllText(_settingsPath, json);

        var loaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: true);

        Assert.Equal(1.0f, loaded.BgmVolume, precision: 4);
        Assert.Equal(0.0f, loaded.SfxVolume, precision: 4);
        Assert.Equal("fr-FR", loaded.Language);
        Assert.False(loaded.FullScreen);
    }

    [Fact]
    public void Load_FallsBackToSupportedDefault_WhenLanguageCodeIsInvalidOrUnsupported()
    {
        const string json = """
            {
              "BgmVolume": 0.5,
              "SfxVolume": 0.6,
              "Language": "xx-INVALID",
              "FullScreen": true
            }
            """;
        File.WriteAllText(_settingsPath, json);

        var loaded = GameSettingsStore.Load(_settingsPath, defaultFullScreen: false);

        Assert.Equal(LocalizationManager.DEFAULT_CULTURE_CODE, loaded.Language);
        Assert.True(LocalizationManager.IsSupportedCulture(loaded.Language));
        Assert.Equal(0.5f, loaded.BgmVolume, precision: 4);
        Assert.Equal(0.6f, loaded.SfxVolume, precision: 4);
    }

    [Fact]
    public void AudioMixerState_DefaultsAre65PercentBgmAnd75PercentSfx_AndPersistedValuesOverrideDefaults()
    {
        var mixer = new AudioMixerState();

        Assert.Equal(0.65f, mixer.BgmVolume, precision: 4);
        Assert.Equal(0.75f, mixer.SfxVolume, precision: 4);
        Assert.Equal(0.65f, mixer.EffectiveBgmVolume, precision: 4);
        Assert.Equal(0.75f, mixer.GetEffectiveSfxVolume(1f), precision: 4);

        var persisted = new GameSettings
        {
            BgmVolume = 0.25f,
            SfxVolume = 0.90f,
            IsMuted = false,
            Language = "en-US",
            FullScreen = false
        };
        GameSettingsStore.Save(_settingsPath, persisted);

        var loaded = GameSettingsStore.Load(_settingsPath);
        mixer.ApplySettings(loaded);

        Assert.Equal(0.25f, mixer.BgmVolume, precision: 4);
        Assert.Equal(0.90f, mixer.SfxVolume, precision: 4);
        Assert.Equal(0.25f, mixer.EffectiveBgmVolume, precision: 4);
        Assert.Equal(0.90f, mixer.GetEffectiveSfxVolume(1f), precision: 4);
    }
}
