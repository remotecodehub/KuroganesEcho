using System.Linq;
using System.Text.Json;
using KuroganesEcho.Core;
using Xunit;

namespace KuroganesEcho.Core.Tests;

public sealed class Act03EncounterStateMachineTests
{
    [Fact]
    public void EncounterFollowsExactOrder_Minotaur3_Seal3Restore_MagesSequentially_Seal4_Credits()
    {
        var save = new SaveProgressState
        {
            CurrentLevelId = "Act_03_C",
            UnlockedLevelId = "Act_03_C",
            SealAshesParts = 2,
            LivesRemaining = 1
        };

        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", save);

        // 1. Minotaur_3 appears first; no mage is active yet
        Assert.Equal(Act03BossStage.Minotaur3, machine.Stage);
        Assert.Equal("Minotaur_3", machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(3, save));
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealVisible(4, save));

        // 2. Defeating Minotaur_3 immediately reveals Ash Seal 3/4 visually and spawns NO mage
        var minotaurDefeat = machine.OnBossDefeated();
        Assert.True(minotaurDefeat.Transitioned);
        Assert.Equal(3, minotaurDefeat.RevealedSealPartIndex);
        Assert.Null(minotaurDefeat.NextBossVariant);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.True(machine.IsSealVisible(3, save));
        Assert.False(machine.IsSealVisible(4, save));

        // Same-frame instant collection is blocked until the reveal grace elapses
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.TryCollectSeal(3, save).Collected);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);

        // After reveal grace elapses, Ash Seal 3/4 is collectible, and still NO wizard spawns on its own
        machine.Update(Act03EncounterStateMachine.SealCollectionDelaySeconds);
        Assert.True(machine.IsSealAvailable(3, save));
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);

        // 3 & 4. Collecting Ash Seal 3/4 restores resources and only then starts Fire Wizard
        var lives = 1;
        var health = 12;
        var stamina = 0.5f;
        var power = 0.2f;
        var powerRecovery = 1.7f;

        var seal3Result = machine.TryCollectSeal(3, save);
        Assert.True(seal3Result.Collected);
        Assert.True(seal3Result.ShouldRestorePlayerResources);
        Assert.Equal("Fire Wizard", seal3Result.NextBossVariant);
        Assert.False(seal3Result.ShouldOpenCredits);
        Assert.Equal(3, save.SealAshesParts);
        Assert.True(save.Act03Seal3RestorationApplied);

        Act03EncounterStateMachine.ApplySeal3ResourceRestoration(
            GameDifficulty.Normal,
            5f,
            ref lives,
            ref health,
            ref stamina,
            ref power,
            ref powerRecovery);

        Assert.Equal(3, lives);
        Assert.Equal(CombatBalance.GetPlayerMaxHealth(GameDifficulty.Normal), health);
        Assert.Equal(5f, stamina);
        Assert.Equal(CombatBalance.PlayerPowerMaximum, power);
        Assert.Equal(0f, powerRecovery);

        // 5. Only Fire Wizard is active
        Assert.Equal(Act03BossStage.FireWizard, machine.Stage);
        Assert.Equal("Fire Wizard", machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(3, save));
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealVisible(4, save));

        // 6a. Defeating Fire Wizard spawns Lightning Mage (not Wanderer Magican, not Seal 4/4)
        var fireDefeat = machine.OnBossDefeated();
        Assert.True(fireDefeat.Transitioned);
        Assert.Equal("Lightning Mage", fireDefeat.NextBossVariant);
        Assert.Null(fireDefeat.RevealedSealPartIndex);
        Assert.Equal(Act03BossStage.LightningMage, machine.Stage);
        Assert.Equal("Lightning Mage", machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(4, save));

        // 6b. Defeating Lightning Mage spawns Wanderer Magican (not Seal 4/4)
        var lightningDefeat = machine.OnBossDefeated();
        Assert.True(lightningDefeat.Transitioned);
        Assert.Equal("Wanderer Magican", lightningDefeat.NextBossVariant);
        Assert.Null(lightningDefeat.RevealedSealPartIndex);
        Assert.Equal(Act03BossStage.WandererMagican, machine.Stage);
        Assert.Equal("Wanderer Magican", machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(4, save));

        // 6c. Defeating Wanderer Magican reveals Ash Seal 4/4
        var wandererDefeat = machine.OnBossDefeated();
        Assert.True(wandererDefeat.Transitioned);
        Assert.Equal(4, wandererDefeat.RevealedSealPartIndex);
        Assert.Null(wandererDefeat.NextBossVariant);
        Assert.Equal(Act03BossStage.AwaitingSeal4, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.True(machine.IsSealVisible(4, save));

        machine.Update(Act03EncounterStateMachine.SealCollectionDelaySeconds);
        Assert.True(machine.IsSealAvailable(4, save));

        // 6d. Collecting Ash Seal 4/4 completes the sequence and opens Credits
        var seal4Result = machine.TryCollectSeal(4, save);
        Assert.True(seal4Result.Collected);
        Assert.False(seal4Result.ShouldRestorePlayerResources);
        Assert.True(seal4Result.ShouldOpenCredits);
        Assert.Equal(Act03BossStage.Completed, machine.Stage);
        Assert.Equal(4, save.SealAshesParts);
        Assert.True(save.Act03FinalBossesCompleted);
    }

    [Fact]
    public void NoWizardCanAppearBeforeAshSeal3IsCollectedEvenAfterLongWait()
    {
        var save = new SaveProgressState { SealAshesParts = 2 };
        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", save);

        // Cannot collect Seal 3 before Minotaur_3 is defeated
        Assert.False(machine.TryCollectSeal(3, save).Collected);
        Assert.Equal(Act03BossStage.Minotaur3, machine.Stage);

        // Defeat Minotaur_3 -> enters AwaitingSeal3, Seal 3 is immediately visible, no wizard active
        machine.OnBossDefeated();
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.True(machine.IsSealVisible(3, save));

        // Simulate 15 full seconds passing without collecting Ash Seal 3/4
        for (var i = 0; i < 150; i++)
            machine.Update(0.1f);

        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.True(machine.IsSealVisible(3, save));
        Assert.True(machine.IsSealAvailable(3, save));

        // Calling OnBossDefeated again while awaiting Seal 3 does not spawn any wizard
        var spuriousDefeat = machine.OnBossDefeated();
        Assert.False(spuriousDefeat.Transitioned);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);

        // Cannot collect Seal 4 while awaiting Seal 3
        Assert.False(machine.TryCollectSeal(4, save).Collected);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
    }

    [Theory]
    [InlineData(0, 1, 125)] // Easy
    [InlineData(1, 2, 100)] // Normal
    [InlineData(2, 5, 80)]  // Hard
    public void Seal3ResourceRestorationSetsLivesTo3AndFullHealthStaminaPower(
        int difficultyValue,
        int initialLives,
        int expectedMaxHealth)
    {
        var difficulty = (GameDifficulty)difficultyValue;
        var lives = initialLives;
        var health = 5;
        var stamina = 0f;
        var power = 0f;
        var recovery = 2.5f;

        Act03EncounterStateMachine.ApplySeal3ResourceRestoration(
            difficulty,
            5f,
            ref lives,
            ref health,
            ref stamina,
            ref power,
            ref recovery);

        Assert.Equal(3, lives);
        Assert.Equal(expectedMaxHealth, health);
        Assert.Equal(5f, stamina);
        Assert.Equal(CombatBalance.PlayerPowerMaximum, power);
        Assert.Equal(0f, recovery);
    }

    [Fact]
    public void Seal3CollectionAndResourceRestorationOccurStrictlyOnceAcrossSaveLoad()
    {
        var save = new SaveProgressState
        {
            CurrentLevelId = "Act_03_C",
            UnlockedLevelId = "Act_03_C",
            SealAshesParts = 2,
            LivesRemaining = 1
        };

        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", save);
        machine.OnBossDefeated();
        machine.Update(Act03EncounterStateMachine.SealCollectionDelaySeconds);

        var firstCollect = machine.TryCollectSeal(3, save);
        Assert.True(firstCollect.Collected);
        Assert.True(firstCollect.ShouldRestorePlayerResources);

        // Attempting to collect Seal 3 a second time is rejected
        var secondCollect = machine.TryCollectSeal(3, save);
        Assert.False(secondCollect.Collected);
        Assert.False(secondCollect.ShouldRestorePlayerResources);

        // Player loses a life against Fire Wizard and saves
        save.LivesRemaining = 2;
        var json = JsonSerializer.Serialize(save);
        var reloadedSave = JsonSerializer.Deserialize<SaveProgressState>(json)!;

        // Reloading Act_03_C resumes at Fire Wizard without re-exposing Seal 3 or re-restoring lives
        var reloadedMachine = new Act03EncounterStateMachine();
        reloadedMachine.InitializeForLevel("Act_03_C", reloadedSave);
        Assert.Equal(Act03BossStage.FireWizard, reloadedMachine.Stage);
        Assert.Equal("Fire Wizard", reloadedMachine.ActiveBossVariant);
        Assert.False(reloadedMachine.IsSealVisible(3, reloadedSave));
        Assert.False(reloadedMachine.IsSealAvailable(3, reloadedSave));
        Assert.False(reloadedMachine.TryCollectSeal(3, reloadedSave).Collected);
        Assert.Equal(2, reloadedSave.LivesRemaining);
    }

    [Fact]
    public void LegacySaveWithSeal3WithoutRestorationFlagStillSpawnsMinotaur3First()
    {
        const string legacyJson = """
            {
              "CurrentLevelId": "Act_03_C",
              "UnlockedLevelId": "Act_03_C",
              "CharacterId": "Kunoichi",
              "Difficulty": 0,
              "LivesRemaining": 5,
              "SealAshesParts": 3,
              "Act03FinalBossesCompleted": false,
              "CheckpointLevelId": "Act_03_C",
              "CheckpointId": 3
            }
            """;

        var legacySave = JsonSerializer.Deserialize<SaveProgressState>(legacyJson)!;
        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", legacySave);

        Assert.Equal(Act03BossStage.Minotaur3, machine.Stage);
        Assert.Equal("Minotaur_3", machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(3, legacySave));

        // Defeating Minotaur_3 reveals Ash Seal 3/4 and still keeps wizards gated
        var defeat = machine.OnBossDefeated();
        Assert.True(defeat.Transitioned);
        Assert.Equal(3, defeat.RevealedSealPartIndex);
        Assert.Null(defeat.NextBossVariant);
        Assert.True(machine.IsSealVisible(3, legacySave));

        machine.Update(Act03EncounterStateMachine.SealCollectionDelaySeconds);
        Assert.True(machine.IsSealAvailable(3, legacySave));

        // Collecting Ash Seal 3/4 applies restoration once and starts Fire Wizard
        var collect = machine.TryCollectSeal(3, legacySave);
        Assert.True(collect.Collected);
        Assert.True(collect.ShouldRestorePlayerResources);
        Assert.Equal("Fire Wizard", collect.NextBossVariant);
        Assert.True(legacySave.Act03Seal3RestorationApplied);
    }

    [Fact]
    public void Minotaur3TilesetAndAct03CMapBossAndSealPositionsAreValid()
    {
        var repoRoot = FindRepoRoot();
        var tilesetPath = System.IO.Path.Combine(
            repoRoot, "src", "KuroganesEcho.Core", "Content", "Tiled", "Tilesets", "Enemies", "Minotaur_3.tsx");
        Assert.True(System.IO.File.Exists(tilesetPath), $"Missing Minotaur_3.tsx at {tilesetPath}");

        var tsxDoc = System.Xml.Linq.XDocument.Load(tilesetPath);
        var tilesetDir = System.IO.Path.GetDirectoryName(tilesetPath)!;
        var states = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var tile in tsxDoc.Root!.Elements("tile"))
        {
            var imageSource = (string)tile.Element("image")?.Attribute("source");
            Assert.False(string.IsNullOrWhiteSpace(imageSource));
            var fullFramePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(tilesetDir, imageSource!));
            Assert.True(System.IO.File.Exists(fullFramePath), $"Missing frame: {fullFramePath}");

            var stateProp = tile.Element("properties")?.Elements("property")
                .FirstOrDefault(p => string.Equals((string)p.Attribute("name"), "State", System.StringComparison.OrdinalIgnoreCase));
            if (stateProp is not null)
            {
                var stateName = (string)stateProp.Attribute("value") ?? string.Empty;
                states.Add(stateName);
                Assert.NotEmpty(tile.Element("animation")?.Elements("frame") ?? System.Linq.Enumerable.Empty<System.Xml.Linq.XElement>());
            }
        }

        Assert.Contains("Idle", states);
        Assert.Contains("Walk", states);
        Assert.Contains("Attack1", states);
        Assert.Contains("Hurt", states);
        Assert.Contains("Dead", states);

        var mapPath = System.IO.Path.Combine(
            repoRoot, "src", "KuroganesEcho.Core", "Content", "Tiled", "Maps", "Act_03_C.tmx");
        var tmxDoc = System.Xml.Linq.XDocument.Load(mapPath);
        var mapProps = tmxDoc.Root!.Element("properties")!.Elements("property")
            .ToDictionary(p => (string)p.Attribute("name")!, p => (string)p.Attribute("value")!, System.StringComparer.OrdinalIgnoreCase);

        // Verify Seal 3/4 is positioned cleanly at (236,19) -> x=3776, y=304 (reachable on ground, not under melee fight at x<=3720)
        Assert.Equal("236,19", mapProps["SealRubyPartTiles"]);
        Assert.Equal("238,19", mapProps["FinalSealRubyPartTiles"]);

        var gameplay = tmxDoc.Root!.Elements("objectgroup")
            .First(g => string.Equals((string)g.Attribute("name"), "Gameplay", System.StringComparison.OrdinalIgnoreCase));

        var bossSpawns = gameplay.Elements("object")
            .Where(o => string.Equals((string)o.Attribute("type"), "BossSpawn", System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(bossSpawns);

        var bossProps = bossSpawns[0].Element("properties")!.Elements("property")
            .ToDictionary(p => (string)p.Attribute("name")!, p => (string)p.Attribute("value")!, System.StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Minotaur", bossProps["BossType"]);
        Assert.Equal("Minotaur_3", bossProps["BossVariant"]);

        var enemyTypes = gameplay.Elements("object")
            .Where(o => string.Equals((string)o.Attribute("type"), "EnemySpawn", System.StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Element("properties")?.Elements("property")
                .FirstOrDefault(p => string.Equals((string)p.Attribute("name"), "EnemyType", System.StringComparison.OrdinalIgnoreCase))?
                .Attribute("value")?.Value)
            .ToList();
        Assert.DoesNotContain("Fire Wizard", enemyTypes);
        Assert.DoesNotContain("Lightning Mage", enemyTypes);
        Assert.DoesNotContain("Wanderer Magican", enemyTypes);
    }

    [Fact]
    public void CompletedSaveReloadsAsCompletedWithNoBoss()
    {
        var save = new SaveProgressState
        {
            CurrentLevelId = "Act_03_C",
            UnlockedLevelId = "Act_03_C",
            SealAshesParts = 4,
            Act03Seal3RestorationApplied = true,
            Act03FinalBossesCompleted = true
        };

        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", save);

        Assert.Equal(Act03BossStage.Completed, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.False(machine.IsSealVisible(3, save));
        Assert.False(machine.IsSealVisible(4, save));
    }

    [Theory]
    [InlineData("Act_01_A")]
    [InlineData("Act_01_C")]
    [InlineData("Act_02_A")]
    [InlineData("Act_02_C")]
    [InlineData("Act_03_A")]
    [InlineData("Act_03_B")]
    public void NonAct03CLevelsDoNotActivateAct03BossSequence(string levelId)
    {
        var save = new SaveProgressState { CurrentLevelId = levelId, SealAshesParts = 2 };
        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel(levelId, save);

        Assert.Equal(Act03BossStage.None, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
    }

    private static string FindRepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "KuroganesEcho.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new System.InvalidOperationException("Could not locate repository root (KuroganesEcho.slnx).");
    }
}
