using System;

namespace KuroganesEcho.Core;

internal enum Act03BossStage
{
    None,
    Minotaur3,
    AwaitingSeal3,
    FireWizard,
    LightningMage,
    WandererMagican,
    AwaitingSeal4,
    Completed
}

internal readonly record struct Act03BossDefeatResult(
    bool Transitioned,
    int? RevealedSealPartIndex = null,
    string NextBossVariant = null);

internal readonly record struct Act03SealCollectionResult(
    bool Collected,
    bool ShouldRestorePlayerResources = false,
    string NextBossVariant = null,
    bool ShouldOpenCredits = false);

/// <summary>
/// Deterministic state machine for the Act 03 C final encounter sequence:
/// Minotaur_3 -> Ash Seal 3/4 (+ one-time resource restoration) ->
/// Fire Wizard -> Lightning Mage -> Wanderer Magican -> Ash Seal 4/4 -> Credits.
/// </summary>
internal sealed class Act03EncounterStateMachine
{
    public const int Seal3RestoredLives = 3;
    public const float SealCollectionDelaySeconds = 0.65f;

    public Act03BossStage Stage { get; private set; } = Act03BossStage.None;
    public float SealRevealGraceRemaining { get; private set; }

    public string ActiveBossVariant => Stage switch
    {
        Act03BossStage.Minotaur3 => "Minotaur_3",
        Act03BossStage.FireWizard => "Fire Wizard",
        Act03BossStage.LightningMage => "Lightning Mage",
        Act03BossStage.WandererMagican => "Wanderer Magican",
        _ => null
    };

    public void InitializeForLevel(string levelId, SaveProgressState save)
    {
        SealRevealGraceRemaining = 0f;

        if (!string.Equals(levelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
        {
            Stage = Act03BossStage.None;
            return;
        }

        if (save is not null && save.Act03FinalBossesCompleted)
        {
            Stage = Act03BossStage.Completed;
            return;
        }

        if (save is not null && save.Act03Seal3RestorationApplied)
        {
            // Seal 3/4 was already collected and restored resources in this save;
            // resume at Fire Wizard without re-awarding Seal 3/4 or restoring resources again.
            Stage = Act03BossStage.FireWizard;
            return;
        }

        Stage = Act03BossStage.Minotaur3;
    }

    public void Update(float elapsedSeconds)
    {
        if (SealRevealGraceRemaining > 0f && elapsedSeconds > 0f)
            SealRevealGraceRemaining = Math.Max(0f, SealRevealGraceRemaining - elapsedSeconds);
    }

    public Act03BossDefeatResult OnBossDefeated()
    {
        switch (Stage)
        {
            case Act03BossStage.Minotaur3:
                Stage = Act03BossStage.AwaitingSeal3;
                SealRevealGraceRemaining = SealCollectionDelaySeconds;
                return new Act03BossDefeatResult(Transitioned: true, RevealedSealPartIndex: 3);

            case Act03BossStage.FireWizard:
                Stage = Act03BossStage.LightningMage;
                return new Act03BossDefeatResult(Transitioned: true, NextBossVariant: "Lightning Mage");

            case Act03BossStage.LightningMage:
                Stage = Act03BossStage.WandererMagican;
                return new Act03BossDefeatResult(Transitioned: true, NextBossVariant: "Wanderer Magican");

            case Act03BossStage.WandererMagican:
                Stage = Act03BossStage.AwaitingSeal4;
                SealRevealGraceRemaining = SealCollectionDelaySeconds;
                return new Act03BossDefeatResult(Transitioned: true, RevealedSealPartIndex: 4);

            default:
                return new Act03BossDefeatResult(Transitioned: false);
        }
    }

    /// <summary>
    /// True immediately once the boss is defeated so the seal renders visibly on screen.
    /// </summary>
    public bool IsSealVisible(int partIndex, SaveProgressState save)
    {
        if (partIndex == 3)
        {
            return Stage == Act03BossStage.AwaitingSeal3 &&
                   (save is null || !save.Act03Seal3RestorationApplied);
        }

        if (partIndex == 4)
            return Stage == Act03BossStage.AwaitingSeal4;

        return false;
    }

    /// <summary>
    /// True only once the seal is visible AND the brief reveal grace period has elapsed,
    /// preventing accidental instant same-frame collection before the player sees the seal.
    /// </summary>
    public bool IsSealAvailable(int partIndex, SaveProgressState save) =>
        IsSealVisible(partIndex, save) && SealRevealGraceRemaining <= 0f;

    public Act03SealCollectionResult TryCollectSeal(int partIndex, SaveProgressState save)
    {
        if (partIndex == 3)
        {
            if (!IsSealAvailable(3, save))
                return new Act03SealCollectionResult(Collected: false);

            var shouldRestore = save is null || !save.Act03Seal3RestorationApplied;
            if (save is not null)
            {
                save.SealAshesParts = Math.Clamp(Math.Max(save.SealAshesParts, 3), 0, 4);
                save.Act03Seal3RestorationApplied = true;
            }

            Stage = Act03BossStage.FireWizard;
            SealRevealGraceRemaining = 0f;
            return new Act03SealCollectionResult(
                Collected: true,
                ShouldRestorePlayerResources: shouldRestore,
                NextBossVariant: "Fire Wizard");
        }

        if (partIndex == 4)
        {
            if (!IsSealAvailable(4, save))
                return new Act03SealCollectionResult(Collected: false);

            Stage = Act03BossStage.Completed;
            SealRevealGraceRemaining = 0f;
            if (save is not null)
            {
                save.SealAshesParts = 4;
                save.Act03FinalBossesCompleted = true;
            }

            return new Act03SealCollectionResult(
                Collected: true,
                ShouldOpenCredits: true);
        }

        return new Act03SealCollectionResult(Collected: false);
    }

    public static void ApplySeal3ResourceRestoration(
        GameDifficulty difficulty,
        float staminaMaximum,
        ref int livesRemaining,
        ref int playerHealth,
        ref float stamina,
        ref float power,
        ref float powerRecoveryElapsed)
    {
        livesRemaining = Seal3RestoredLives;
        playerHealth = CombatBalance.GetPlayerMaxHealth(difficulty);
        stamina = staminaMaximum;
        power = CombatBalance.PlayerPowerMaximum;
        powerRecoveryElapsed = 0f;
    }
}
