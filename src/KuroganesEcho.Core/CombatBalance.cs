namespace KuroganesEcho.Core;

/// <summary>
/// Central combat values shared by players, enemies and health-bar rendering.
/// </summary>
internal static class CombatBalance
{
    public const int PlayerMaxHealth = 100;
    public const int EnemyMaxHealth = 80;
    public const int BossMaxHealth = 300;
    public const int BossRecoveryThreshold = 150;
    public const int BossRecoveryHealth = 250;
    public const int PlayerAttackDamage = 8;
    public const int StrongAttackDamage = 14;
    public const int SpecialAttackDamage = 20;
    public const int MonkBladeDamage = 30;
    public const int EnemyAttackDamage = 10;
    public const int PlayerPowerMaximum = 3;
    public const float StrongAttackStaminaCost = 0.75f;
    public const float LowHealthProtectionThresholdFraction = 0.10f;
    public const float LowHealthDamageMultiplier = 0.75f;
    public const float LowHealthProtectionDurationSeconds = 3f;
    public const float SpecialPowerCost = 0.75f;
    public const float PowerFruitRestore = 1f;
    public const float PlayerPowerRecoveryIntervalSeconds = 3f;
    public const float KunoichiFruitStaminaRestore = 2.5f;
    public const float KunoichiFruitHealthRestoreFraction = 0.25f;

    public static int AdjustPlayerAttackDamage(int damage, GameDifficulty difficulty) =>
        difficulty == GameDifficulty.Easy
            ? (int)System.MathF.Ceiling(damage * 1.25f)
            : damage;

    public static float GetPlayerPowerRecoveryAmount(GameDifficulty difficulty) =>
        PlayerPowerMaximum * (difficulty switch
        {
            GameDifficulty.Easy => 0.20f,
            GameDifficulty.Hard => 0.05f,
            _ => 0.10f
        });

    public static int GetPlayerMaxHealth(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => 125,
        GameDifficulty.Hard => 80,
        _ => PlayerMaxHealth
    };

    public static int GetEnemyMaxHealth(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => 60,
        GameDifficulty.Hard => 120,
        _ => EnemyMaxHealth
    };

    public static int GetBossMaxHealth(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => BossMaxHealth,
        GameDifficulty.Hard => BossMaxHealth,
        _ => BossMaxHealth
    };
}
