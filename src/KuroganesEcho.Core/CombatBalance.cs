namespace KuroganesEcho.Core;

/// <summary>
/// Central combat values shared by players, enemies and health-bar rendering.
/// </summary>
internal static class CombatBalance
{
    public const int PlayerMaxHealth = 100;
    public const int EnemyMaxHealth = 80;
    public const int BossMaxHealth = 400;
    public const int PlayerAttackDamage = 8;
    public const int SpecialAttackDamage = 14;
    public const int MonkBladeDamage = 24;
    public const int EnemyAttackDamage = 10;
    public const int PlayerPowerMaximum = 3;
    public const float SpecialPowerCost = 1f;
    public const float PowerFruitRestore = 1f;
    public const float PlayerPowerRecoveryIntervalSeconds = 3f;
    public const float KunoichiFruitStaminaRestore = 2.5f;
    public const float KunoichiFruitHealthRestoreFraction = 0.25f;

    public static float GetSecondarySpecialPowerCost(GameDifficulty difficulty) =>
        PlayerPowerMaximum * (difficulty switch
        {
            GameDifficulty.Easy => 0.05f,
            GameDifficulty.Hard => 0.10f,
            _ => 0.075f
        });

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
        GameDifficulty.Easy => 320,
        GameDifficulty.Hard => 520,
        _ => BossMaxHealth
    };
}
