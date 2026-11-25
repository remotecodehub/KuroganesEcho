using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal enum BossKind
{
    Minotaur,
    Wizard
}

/// <summary>
/// Reusable boss presentation and lifecycle. Bosses are deliberately separate
/// from regular enemies so future acts can give them their own encounters,
/// health bars and phase rules without changing <see cref="EnemyInstance"/>.
/// </summary>
internal sealed class BossInstance
{
    private const float BossScale = 0.85f;
    private const float AttackDistance = 112f;
    private const float AttackDuration = 0.8f;
    private const float AttackCooldown = 1.6f;
    private const float BossHalfWidth = 16f;
    private const float BossHeight = 48f;

    private float _attackRemaining;
    private float _attackCooldown;
    private bool _attackHitApplied;
    private int _facing = 1;
    private GameDifficulty _difficulty = GameDifficulty.Normal;

    public BossInstance(
        ContentManager content,
        BossKind kind,
        LevelObjectData spawn,
        string variant = null)
    {
        Kind = kind;
        Spawn = spawn;
        VariantId = variant ?? EnemyCharacter.DefaultBossVariant(kind.ToString());
        if (!EnemyCharacter.IsBossType(VariantId) || !IsVariantOf(Kind, VariantId))
            throw new ArgumentOutOfRangeException(nameof(variant), VariantId, "Unknown boss variant");

        Character = EnemyCharacter.Load(content, VariantId);
        Position = new Vector2(spawn.Bounds.Center.X, spawn.Bounds.Bottom);
        Health = MaxHealth;
    }

    public BossKind Kind { get; }
    public string VariantId { get; }
    public LevelObjectData Spawn { get; }
    public EnemyCharacter Character { get; }
    public Vector2 Position { get; private set; }
    public int MaxHealth => CombatBalance.GetBossMaxHealth(_difficulty);
    public int Health { get; private set; }
    public bool IsAlive => Health > 0;
    public bool IsEnraged => IsAlive && Health <= MaxHealth / 2;
    public bool IsAttacking => IsAlive && _attackRemaining > 0f;

    public void ConfigureDifficulty(GameDifficulty difficulty)
    {
        _difficulty = difficulty;
        Health = Math.Min(Health, MaxHealth);
    }
    public Rectangle Hitbox => new(
        (int)(Position.X - BossHalfWidth),
        (int)(Position.Y - BossHeight),
        (int)(BossHalfWidth * 2f),
        (int)BossHeight);

    private Rectangle AttackHitbox => new(
        (int)(Position.X + (_facing > 0 ? 6f : -54f)),
        (int)(Position.Y - 40f),
        48,
        30);

    public static bool TryCreate(
        ContentManager content,
        LevelObjectData spawn,
        out BossInstance boss)
    {
        boss = null;
        if (!spawn.Properties.TryGetValue("BossType", out var bossType))
            return false;

        if (!Enum.TryParse<BossKind>(bossType, ignoreCase: true, out var kind))
            return false;

        var variant = spawn.Properties.TryGetValue("BossVariant", out var configuredVariant)
            ? configuredVariant
            : null;
        boss = new BossInstance(content, kind, spawn, variant);
        return true;
    }

    public void Reset()
    {
        Position = new Vector2(Spawn.Bounds.Center.X, Spawn.Bounds.Bottom);
        Health = MaxHealth;
        _attackRemaining = 0f;
        _attackCooldown = 0f;
        _attackHitApplied = false;
        _facing = 1;
        Character.Reset();
    }

    public void Update(float elapsedSeconds, Vector2 playerPosition)
    {
        _attackCooldown = Math.Max(0f, _attackCooldown - elapsedSeconds);

        if (!IsAlive)
        {
            Character.Update(elapsedSeconds);
            return;
        }

        var horizontalDistance = playerPosition.X - Position.X;
        _facing = horizontalDistance < 0f ? -1 : 1;
        if (_attackRemaining > 0f)
        {
            _attackRemaining = Math.Max(0f, _attackRemaining - elapsedSeconds);
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else if (Math.Abs(horizontalDistance) <= AttackDistance && _attackCooldown <= 0f)
        {
            _attackRemaining = AttackDuration;
            _attackCooldown = AttackCooldown;
            _attackHitApplied = false;
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else
        {
            Character.SetAnimation(EnemyAnimationState.Idle);
        }

        Character.Update(elapsedSeconds);
    }

    public bool TryConsumeAttackHit(Rectangle playerHitbox)
    {
        if (!IsAttacking || _attackHitApplied || !AttackHitbox.Intersects(playerHitbox))
            return false;

        _attackHitApplied = true;
        return true;
    }

    public bool TakeDamage(int damage, float hitPointY)
    {
        if (!IsAlive)
            return false;

        var hitHeight = Math.Clamp((Position.Y - hitPointY) / BossHeight, 0f, 1f);
        var heightMultiplier = 0.75f + hitHeight * 0.5f;
        Health = Math.Max(0, Health - Math.Max(1, (int)Math.Round(damage * heightMultiplier)));
        Character.SetAnimation(IsAlive ? EnemyAnimationState.Hurt : EnemyAnimationState.Dead);
        return true;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var effects = _facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Character.Draw(spriteBatch, Position, BossScale, effects);
    }

    private static bool IsVariantOf(BossKind kind, string variant) => kind switch
    {
        BossKind.Minotaur => variant is "Minotaur_1" or "Minotaur_2" or "Minotaur_3",
        BossKind.Wizard => variant is "Fire Wizard" or "Lightning Mage" or "Wanderer Magican",
        _ => false
    };
}
