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
    public bool Act03Seal3RestorationApplied { get; set; }
    public bool Act03FinalBossesCompleted { get; set; }
    public string CheckpointLevelId { get; set; } = "Act_01_A";
    public int CheckpointId { get; set; }
}

internal static class SaveProgressStore
{
    private const string DefaultLevelId = "Act_01_A";
    public const int SlotCount = 3;

    public static string StorageDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();
            return Path.Combine(root, "KuroganesEcho");
        }
    }

    public static string FilePath => Path.Combine(StorageDirectory, "progress.json");

    public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

    public static SaveProgressState Load() => Load(StorageDirectory);

    public static SaveProgressState Load(string directory)
    {
        try
        {
            var path = Path.Combine(directory, "progress.json");
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

    public static bool HasProgress(SaveProgressState state) =>
        !string.Equals(state.CurrentLevelId, DefaultLevelId, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(state.UnlockedLevelId, DefaultLevelId, StringComparison.OrdinalIgnoreCase) ||
        state.SealAshesParts > 0 ||
        state.Act03FinalBossesCompleted;

    public static bool HasSlot(int slot) => HasSlot(slot, StorageDirectory);

    public static bool HasSlot(int slot, string directory)
    {
        if (!IsValidSlot(slot) || string.IsNullOrWhiteSpace(directory))
            return false;

        return File.Exists(GetSlotPath(slot, directory));
    }

    public static SaveProgressState LoadSlot(int slot) => LoadSlot(slot, StorageDirectory);

    public static SaveProgressState LoadSlot(int slot, string directory)
    {
        if (!IsValidSlot(slot) || string.IsNullOrWhiteSpace(directory))
            return new SaveProgressState();

        var path = GetSlotPath(slot, directory);
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

    public static void SaveSlot(int slot, SaveProgressState state) =>
        SaveSlot(slot, state, StorageDirectory);

    public static void SaveSlot(int slot, SaveProgressState state, string directory)
    {
        if (!IsValidSlot(slot) || state is null || string.IsNullOrWhiteSpace(directory))
            return;

        try
        {
            var path = GetSlotPath(slot, directory);
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // A read-only or unavailable profile must not stop the game.
        }
    }

    public static bool DeleteSlot(int slot) => DeleteSlot(slot, StorageDirectory);

    public static bool DeleteSlot(int slot, string directory)
    {
        if (!IsValidSlot(slot) || string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            var path = GetSlotPath(slot, directory);
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(SaveProgressState state) => Save(state, StorageDirectory);

    public static void Save(SaveProgressState state, string directory)
    {
        if (state is null || string.IsNullOrWhiteSpace(directory))
            return;

        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "progress.json");
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // A read-only or unavailable profile must not stop the game.
        }
    }

    internal static string GetSlotPath(int slot, string directory) =>
        Path.Combine(directory, $"memory-card-slot-{slot}.json");
}
