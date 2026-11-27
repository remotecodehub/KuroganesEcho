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
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealAvailable(4, save));

        // 2. Defeating Minotaur_3 reveals Ash Seal 3/4 and does NOT spawn any mage yet
        var minotaurDefeat = machine.OnBossDefeated();
        Assert.True(minotaurDefeat.Transitioned);
        Assert.Equal(3, minotaurDefeat.RevealedSealPartIndex);
        Assert.Null(minotaurDefeat.NextBossVariant);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
        Assert.True(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealAvailable(4, save));

        // 3 & 4. Collecting Ash Seal 3/4 restores resources and starts Fire Wizard
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
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealAvailable(4, save));

        // 6a. Defeating Fire Wizard spawns Lightning Mage (not Wanderer Magican, not Seal 4/4)
        var fireDefeat = machine.OnBossDefeated();
        Assert.True(fireDefeat.Transitioned);
        Assert.Equal("Lightning Mage", fireDefeat.NextBossVariant);
        Assert.Null(fireDefeat.RevealedSealPartIndex);
        Assert.Equal(Act03BossStage.LightningMage, machine.Stage);
        Assert.Equal("Lightning Mage", machine.ActiveBossVariant);
        Assert.False(machine.IsSealAvailable(4, save));

        // 6b. Defeating Lightning Mage spawns Wanderer Magican (not Seal 4/4)
        var lightningDefeat = machine.OnBossDefeated();
        Assert.True(lightningDefeat.Transitioned);
        Assert.Equal("Wanderer Magican", lightningDefeat.NextBossVariant);
        Assert.Null(lightningDefeat.RevealedSealPartIndex);
        Assert.Equal(Act03BossStage.WandererMagican, machine.Stage);
        Assert.Equal("Wanderer Magican", machine.ActiveBossVariant);
        Assert.False(machine.IsSealAvailable(4, save));

        // 6c. Defeating Wanderer Magican reveals Ash Seal 4/4
        var wandererDefeat = machine.OnBossDefeated();
        Assert.True(wandererDefeat.Transitioned);
        Assert.Equal(4, wandererDefeat.RevealedSealPartIndex);
        Assert.Null(wandererDefeat.NextBossVariant);
        Assert.Equal(Act03BossStage.AwaitingSeal4, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);
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
    public void MageSequenceCannotStartBeforeAshSeal3IsCollected()
    {
        var save = new SaveProgressState { SealAshesParts = 2 };
        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", save);

        // Cannot collect Seal 3 before Minotaur_3 is defeated
        Assert.False(machine.TryCollectSeal(3, save).Collected);
        Assert.Equal(Act03BossStage.Minotaur3, machine.Stage);

        // Defeat Minotaur_3 -> enters AwaitingSeal3
        machine.OnBossDefeated();
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);

        // Calling OnBossDefeated again while awaiting Seal 3 does not spawn Fire Wizard
        var spuriousDefeat = machine.OnBossDefeated();
        Assert.False(spuriousDefeat.Transitioned);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
        Assert.Null(machine.ActiveBossVariant);

        // Cannot collect Seal 4 while awaiting Seal 3
        Assert.False(machine.TryCollectSeal(4, save).Collected);
        Assert.Equal(Act03BossStage.AwaitingSeal3, machine.Stage);
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
        Assert.False(reloadedMachine.IsSealAvailable(3, reloadedSave));
        Assert.False(reloadedMachine.TryCollectSeal(3, reloadedSave).Collected);
        Assert.Equal(2, reloadedSave.LivesRemaining);
    }

    [Fact]
    public void LegacySaveWithSeal3ResumesAtFireWizardWithoutReTriggeringRestoration()
    {
        // Simulates a save file created before Act03Seal3RestorationApplied existed
        const string legacyJson = """
            {
              "CurrentLevelId": "Act_03_C",
              "UnlockedLevelId": "Act_03_C",
              "CharacterId": "Kunoichi",
              "Difficulty": 1,
              "LivesRemaining": 2,
              "SealAshesParts": 3,
              "CheckpointLevelId": "Act_03_C",
              "CheckpointId": 3
            }
            """;

        var legacySave = JsonSerializer.Deserialize<SaveProgressState>(legacyJson)!;
        var machine = new Act03EncounterStateMachine();
        machine.InitializeForLevel("Act_03_C", legacySave);

        Assert.Equal(Act03BossStage.FireWizard, machine.Stage);
        Assert.Equal("Fire Wizard", machine.ActiveBossVariant);
        Assert.False(machine.IsSealAvailable(3, legacySave));
        Assert.False(machine.TryCollectSeal(3, legacySave).Collected);
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
        Assert.False(machine.IsSealAvailable(3, save));
        Assert.False(machine.IsSealAvailable(4, save));
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
}
