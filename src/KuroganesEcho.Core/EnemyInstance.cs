using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

/// <summary>
/// Reusable enemy physics, patrol, attack and health state.
/// </summary>
internal sealed class EnemyInstance
{
    private const float PatrolHalfWidth = 72f;
    private const float AttackDistance = 78f;
    private const float RangedAttackDuration = 0.75f;
    private const float RangedAttackCooldown = 2f;
    private const float RangedAttackReleaseRemaining = 0.25f;
    private const float RangedAttackVerticalTolerance = 48f;
    private const float StunDuration = 1f;
    private const int HitsToStun = 2;
    private const float Gravity = 480f;
    private const float EnemyHalfWidth = 10f;
    private const float EnemyHeight = 32f;

    private readonly float _patrolLeft;
    private readonly float _patrolRight;
    private float _attackRemaining;
    private float _attackCooldown;
    private float _damageInvulnerability;
    private float _stunRemaining;
    private float _verticalVelocity;
    private int _hitsSinceLastStun;
    private bool _attackConsumed;
    private bool _rangedAttack;
    private int _facing = 1;
    private GameDifficulty _difficulty = GameDifficulty.Normal;

    public EnemyInstance(EnemyCharacter character, LevelObjectData spawn)
    {
        Character = character;
        Spawn = spawn;
        Position = new Vector2(spawn.Bounds.Center.X, spawn.Bounds.Bottom);
        _patrolLeft = Position.X - PatrolHalfWidth;
        _patrolRight = Position.X + PatrolHalfWidth;
        Health = MaxHealth;
    }

    public EnemyCharacter Character { get; }
    public LevelObjectData Spawn { get; }
    public Vector2 Position { get; private set; }
    public int MaxHealth => CombatBalance.GetEnemyMaxHealth(_difficulty);
    public int Health { get; private set; }
    public bool IsAlive => Health > 0;
    public bool IsStunned => _stunRemaining > 0f;
    public bool IsDeathAnimationComplete => !IsAlive && Character.IsDeadAnimationComplete;
    public bool IsAttacking => IsAlive && _attackRemaining > 0f;
    public bool IsRangedAttacking => IsAttacking && _rangedAttack;

    public void ConfigureDifficulty(GameDifficulty difficulty)
    {
        _difficulty = difficulty;
        Health = Math.Min(Health, MaxHealth);
    }
    public Rectangle Hitbox => new(
        (int)(Position.X - EnemyHalfWidth),
        (int)(Position.Y - EnemyHeight),
        (int)(EnemyHalfWidth * 2f),
        (int)EnemyHeight);

    private Rectangle AttackHitbox => new(
        (int)(Position.X + (_facing > 0 ? 4f : -36f)),
        (int)(Position.Y - 28f),
        32,
        22);

    public void Reset()
    {
        Position = new Vector2(Spawn.Bounds.Center.X, Spawn.Bounds.Bottom);
        Health = MaxHealth;
        _attackRemaining = 0f;
        _attackCooldown = 0f;
        _damageInvulnerability = 0f;
        _stunRemaining = 0f;
        _verticalVelocity = 0f;
        _hitsSinceLastStun = 0;
        _attackConsumed = false;
        _rangedAttack = false;
        _facing = 1;
        Character.Reset();
    }

    public void Update(float elapsedSeconds, Vector2 playerPosition, GameLevel level, Rectangle visibleBounds)
    {
        _attackCooldown = Math.Max(0f, _attackCooldown - elapsedSeconds);
        _damageInvulnerability = Math.Max(0f, _damageInvulnerability - elapsedSeconds);
        _stunRemaining = Math.Max(0f, _stunRemaining - elapsedSeconds);

        if (!IsAlive)
        {
            Character.Update(elapsedSeconds);
            return;
        }

        var horizontalDistance = playerPosition.X - Position.X;
        if (IsStunned)
        {
            _attackRemaining = 0f;
            _rangedAttack = false;
            Character.SetAnimation(EnemyAnimationState.Hurt);
        }
        else if (_attackRemaining > 0f)
        {
            _attackRemaining = Math.Max(0f, _attackRemaining - elapsedSeconds);
            _facing = horizontalDistance < 0f ? -1 : 1;
            Character.SetAnimation(_rangedAttack ? Character.RangedAttackAnimation : EnemyAnimationState.Attack1);
        }
        else if (Character.HasRangedAttack &&
                 visibleBounds.Intersects(Hitbox) &&
                 Math.Abs(horizontalDistance) > AttackDistance &&
                 Math.Abs(playerPosition.Y - Position.Y) <= RangedAttackVerticalTolerance &&
                 _attackCooldown <= 0f)
        {
            _attackRemaining = RangedAttackDuration;
            _attackCooldown = RangedAttackCooldown;
            _attackConsumed = false;
            _rangedAttack = true;
            _facing = horizontalDistance < 0f ? -1 : 1;
            Character.SetAnimation(Character.RangedAttackAnimation);
        }
        else if (Math.Abs(horizontalDistance) <= AttackDistance && _attackCooldown <= 0f)
        {
            _attackRemaining = 0.55f;
            _attackCooldown = 1.4f;
            _attackConsumed = false;
            _rangedAttack = false;
            _facing = horizontalDistance < 0f ? -1 : 1;
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else
        {
            _rangedAttack = false;
            var speed = level.Difficulty switch
            {
                GameDifficulty.Easy => 12f,
                GameDifficulty.Hard => 24f,
                _ => 18f
            };

            Position = new Vector2(Position.X + _facing * speed * elapsedSeconds, Position.Y);
            if (Position.X <= _patrolLeft)
            {
                Position = new Vector2(_patrolLeft, Position.Y);
                _facing = 1;
            }
            else if (Position.X >= _patrolRight)
            {
                Position = new Vector2(_patrolRight, Position.Y);
                _facing = -1;
            }

            Character.SetAnimation(Math.Abs(horizontalDistance) > AttackDistance * 2f
                ? EnemyAnimationState.Walk
                : EnemyAnimationState.Idle);
        }

        var previousY = Position.Y;
        _verticalVelocity += Gravity * elapsedSeconds;
        Position = new Vector2(Position.X, Position.Y + _verticalVelocity * elapsedSeconds);
        var landingTop = level.FindLandingTop(Position.X, previousY, Position.Y, EnemyHalfWidth);
        if (_verticalVelocity >= 0f && landingTop.HasValue)
        {
            Position = new Vector2(Position.X, landingTop.Value);
            _verticalVelocity = 0f;
        }
        else if (Position.Y > level.WorldBounds.Height + EnemyHeight)
        {
            Position = new Vector2(Position.X, Spawn.Bounds.Bottom);
            _verticalVelocity = 0f;
        }

        Character.Update(elapsedSeconds);
    }

    public bool TryLaunchRangedAttack(Vector2 targetPosition, out Vector2 origin, out Vector2 direction)
    {
        origin = default;
        direction = default;
        if (!IsRangedAttacking || _attackConsumed || _attackRemaining > RangedAttackReleaseRemaining)
            return false;

        _attackConsumed = true;
        origin = Position + new Vector2(_facing * (EnemyHalfWidth + 3f), -EnemyHeight * 0.7f);
        direction = targetPosition - origin;
        direction = direction.LengthSquared() > 0.001f
            ? Vector2.Normalize(direction)
            : new Vector2(_facing, 0f);
        return true;
    }

    public bool TryConsumeAttackHit(Rectangle playerHitbox)
    {
        if (!IsAttacking || _rangedAttack || _attackConsumed || !AttackHitbox.Intersects(playerHitbox))
            return false;

        _attackConsumed = true;
        return true;
    }

    public bool TakeDamage(int damage, float attackerX)
    {
        if (!IsAlive || _damageInvulnerability > 0f)
            return false;

        Health = Math.Max(0, Health - damage);
        _damageInvulnerability = 0.2f;
        _facing = attackerX < Position.X ? 1 : -1;
        _verticalVelocity = -65f;
        Position = new Vector2(Position.X + (attackerX < Position.X ? 8f : -8f), Position.Y);
        if (!IsStunned && ++_hitsSinceLastStun >= HitsToStun)
        {
            _hitsSinceLastStun = 0;
            _stunRemaining = StunDuration;
            _attackRemaining = 0f;
            _rangedAttack = false;
        }
        if (!IsAlive)
        {
            _stunRemaining = 0f;
            _hitsSinceLastStun = 0;
        }
        Character.SetAnimation(IsAlive ? EnemyAnimationState.Hurt : EnemyAnimationState.Dead);
        return true;
    }

    public void PushHorizontally(float amount)
    {
        if (IsAlive)
            Position = new Vector2(Position.X + amount, Position.Y);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var effects = _facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        var scale = _difficulty switch
        {
            GameDifficulty.Easy => 0.5f,
            GameDifficulty.Hard => 0.7f,
            _ => 0.6f
        };
        Character.Draw(spriteBatch, Position, scale, effects);
    }
}
