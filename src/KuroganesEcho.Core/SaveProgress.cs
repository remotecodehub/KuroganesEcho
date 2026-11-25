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
    public int IronTalismanPieces { get; set; }
}

internal static class SaveProgressStore
{
    private const string DefaultLevelId = "Act_01_A";

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
        !string.Equals(state.UnlockedLevelId, DefaultLevelId, StringComparison.OrdinalIgnoreCase);

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
}
