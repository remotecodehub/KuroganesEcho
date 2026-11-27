using System;
using System.IO;
using System.Text.Json;

namespace KuroganesEcho.Core;

internal sealed class SaveProgressState
{
    public string CurrentLevelId { get; set; } = "Act_01_A";
    public string UnlockedLevelId { get; set; } = "Act_01_A";
    public string CharacterId { get; set; } = "Kunoichi";
    public GameDifficulty Difficulty { get; set; } = GameDifficulty.Normal;
    public int LivesRemaining { get; set; } = 3;
    public int SealAshesParts { get; set; }
    public bool Act03FinalBossesCompleted { get; set; }
    public string CheckpointLevelId { get; set; } = "Act_01_A";
    public int CheckpointId { get; set; }
}

internal static class SaveProgressStore
{
    private const string DefaultLevelId = "Act_01_A";
    public const int SlotCount = 3;

    public static string FilePath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();
            return Path.Combine(root, "KuroganesEcho", "progress.json");
        }
    }

    public static SaveProgressState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new SaveProgressState();

            var json = File.ReadAllText(FilePath);
            var state = JsonSerializer.Deserialize<SaveProgressState>(json);
            return state ?? new SaveProgressState();
        }
        catch
        {
            return new SaveProgressState();
        }
    }

    public static bool HasProgress(SaveProgressState state) =>
        !string.Equals(state.CurrentLevelId, DefaultLevelId, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(state.UnlockedLevelId, DefaultLevelId, StringComparison.OrdinalIgnoreCase) ||
        state.SealAshesParts > 0 ||
        state.Act03FinalBossesCompleted;

    public static bool HasSlot(int slot) => File.Exists(GetSlotPath(slot));

    public static SaveProgressState LoadSlot(int slot)
    {
        var path = GetSlotPath(slot);
        try
        {
            if (!File.Exists(path))
                return new SaveProgressState();

            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<SaveProgressState>(json);
            return state ?? new SaveProgressState();
        }
        catch
        {
            return new SaveProgressState();
        }
    }

    public static void SaveSlot(int slot, SaveProgressState state)
    {
        if (slot < 1 || slot > SlotCount)
            return;

        try
        {
            var path = GetSlotPath(slot);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // A read-only or unavailable profile must not stop the game.
        }
    }

    public static void Save(SaveProgressState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // A read-only or unavailable profile must not stop the game.
        }
    }

    private static string GetSlotPath(int slot) =>
        Path.Combine(Path.GetDirectoryName(FilePath) ?? Path.GetTempPath(), $"memory-card-slot-{slot}.json");
}
