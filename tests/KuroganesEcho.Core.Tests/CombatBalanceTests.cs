using KuroganesEcho.Core;
using Xunit;

namespace KuroganesEcho.Core.Tests;

public sealed class CombatBalanceTests
{
    [Theory]
    [InlineData(8, 10)]
    [InlineData(14, 18)]
    [InlineData(20, 25)]
    [InlineData(30, 38)]
    public void EasyDifficultyIncreasesPlayerAttackDamage(int baseDamage, int expectedDamage)
    {
        Assert.Equal(
            expectedDamage,
            CombatBalance.AdjustPlayerAttackDamage(baseDamage, GameDifficulty.Easy));
    }

    [Fact]
    public void NormalDifficultyKeepsPlayerAttackDamage()
    {
        Assert.Equal(14, CombatBalance.AdjustPlayerAttackDamage(14, GameDifficulty.Normal));
    }

    [Fact]
    public void HardDifficultyKeepsPlayerAttackDamage()
    {
        Assert.Equal(14, CombatBalance.AdjustPlayerAttackDamage(14, GameDifficulty.Hard));
    }
}
