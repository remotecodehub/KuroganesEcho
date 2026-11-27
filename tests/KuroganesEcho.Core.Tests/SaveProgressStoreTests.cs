using System;
using System.IO;
using KuroganesEcho.Core;
using Xunit;

namespace KuroganesEcho.Core.Tests;

public sealed class SaveProgressStoreTests : IDisposable
{
    private readonly string _tempDir;

    public SaveProgressStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"KuroganesEcho_SaveTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
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
            // Ignore temp cleanup failures.
        }
    }

    [Fact]
    public void SaveAndLoadIndividualSlots_PreservesDistinctStatePerSlot()
    {
        var slot1State = new SaveProgressState
        {
            CurrentLevelId = "Act_01_C",
            UnlockedLevelId = "Act_01_C",
            CharacterId = "Kunoichi",
            Difficulty = GameDifficulty.Easy,
            LivesRemaining = 4,
            SealAshesParts = 1
        };
        var slot2State = new SaveProgressState
        {
            CurrentLevelId = "Act_03_B",
            UnlockedLevelId = "Act_03_B",
            CharacterId = "Ninja_Monk",
            Difficulty = GameDifficulty.Hard,
            LivesRemaining = 2,
            SealAshesParts = 2
        };

        SaveProgressStore.SaveSlot(1, slot1State, _tempDir);
        SaveProgressStore.SaveSlot(2, slot2State, _tempDir);

        Assert.True(SaveProgressStore.HasSlot(1, _tempDir));
        Assert.True(SaveProgressStore.HasSlot(2, _tempDir));
        Assert.False(SaveProgressStore.HasSlot(3, _tempDir));

        var loaded1 = SaveProgressStore.LoadSlot(1, _tempDir);
        var loaded2 = SaveProgressStore.LoadSlot(2, _tempDir);

        Assert.Equal("Act_01_C", loaded1.CurrentLevelId);
        Assert.Equal("Kunoichi", loaded1.CharacterId);
        Assert.Equal(GameDifficulty.Easy, loaded1.Difficulty);
        Assert.Equal(4, loaded1.LivesRemaining);

        Assert.Equal("Act_03_B", loaded2.CurrentLevelId);
        Assert.Equal("Ninja_Monk", loaded2.CharacterId);
        Assert.Equal(GameDifficulty.Hard, loaded2.Difficulty);
        Assert.Equal(2, loaded2.LivesRemaining);
    }

    [Fact]
    public void DeleteSlot_DeletesOccupiedSlotWithoutAffectingOtherSlotsOrGlobalSettings()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        GameSettingsStore.Save(settingsPath, new GameSettings
        {
            BgmVolume = 0.33f,
            SfxVolume = 0.77f,
            Language = "fr-FR",
            FullScreen = false
        });

        SaveProgressStore.Save(new SaveProgressState { CurrentLevelId = "Act_02_B" }, _tempDir);
        SaveProgressStore.SaveSlot(1, new SaveProgressState { CurrentLevelId = "Act_01_B" }, _tempDir);
        SaveProgressStore.SaveSlot(2, new SaveProgressState { CurrentLevelId = "Act_02_C" }, _tempDir);
        SaveProgressStore.SaveSlot(3, new SaveProgressState { CurrentLevelId = "Act_03_A" }, _tempDir);

        var deleted = SaveProgressStore.DeleteSlot(2, _tempDir);

        Assert.True(deleted);
        Assert.False(SaveProgressStore.HasSlot(2, _tempDir));
        Assert.True(SaveProgressStore.HasSlot(1, _tempDir));
        Assert.True(SaveProgressStore.HasSlot(3, _tempDir));
        Assert.Equal("Act_01_B", SaveProgressStore.LoadSlot(1, _tempDir).CurrentLevelId);
        Assert.Equal("Act_03_A", SaveProgressStore.LoadSlot(3, _tempDir).CurrentLevelId);

        // Global progress and global settings remain intact
        Assert.Equal("Act_02_B", SaveProgressStore.Load(_tempDir).CurrentLevelId);
        var settings = GameSettingsStore.Load(settingsPath);
        Assert.Equal(0.33f, settings.BgmVolume, precision: 4);
        Assert.Equal("fr-FR", settings.Language);
        Assert.False(settings.FullScreen);
    }

    [Fact]
    public void DeleteSlot_OnNonExistentSlot_ReturnsFalseWithoutThrowing()
    {
        Assert.False(SaveProgressStore.HasSlot(1, _tempDir));
        var deleted = SaveProgressStore.DeleteSlot(1, _tempDir);
        Assert.False(deleted);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(99)]
    public void DeleteSlot_WithInvalidSlotIndex_ReturnsFalseAndNeverDeletesUnrelatedFiles(int invalidSlot)
    {
        var unrelatedPath = SaveProgressStore.GetSlotPath(invalidSlot, _tempDir);
        File.WriteAllText(unrelatedPath, "unrelated");
        SaveProgressStore.SaveSlot(1, new SaveProgressState { CurrentLevelId = "Act_01_B" }, _tempDir);

        var deleted = SaveProgressStore.DeleteSlot(invalidSlot, _tempDir);

        Assert.False(deleted);
        Assert.True(File.Exists(unrelatedPath), "Invalid slot index must never delete files.");
        Assert.True(SaveProgressStore.HasSlot(1, _tempDir));
    }
}
