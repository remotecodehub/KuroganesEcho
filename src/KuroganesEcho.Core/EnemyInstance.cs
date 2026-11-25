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
    private const float EnemyScale = 0.5f;
    private const float Gravity = 480f;
    private const float EnemyHalfWidth = 10f;
    private const float EnemyHeight = 32f;

    private readonly float _patrolLeft;
    private readonly float _patrolRight;
    private float _attackRemaining;
    private float _attackCooldown;
    private float _damageInvulnerability;
    private float _verticalVelocity;
    private bool _attackHitApplied;
    private int _facing = 1;

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
    public int MaxHealth => 20;
    public int Health { get; private set; }
    public bool IsAlive => Health > 0;
    public bool IsAttacking => IsAlive && _attackRemaining > 0f;
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
        _verticalVelocity = 0f;
        _attackHitApplied = false;
        _facing = 1;
        Character.Reset();
    }

    public void Update(float elapsedSeconds, Vector2 playerPosition, GameLevel level)
    {
        _attackCooldown = Math.Max(0f, _attackCooldown - elapsedSeconds);
        _damageInvulnerability = Math.Max(0f, _damageInvulnerability - elapsedSeconds);

        if (!IsAlive)
        {
            Character.Update(elapsedSeconds);
            return;
        }

        var horizontalDistance = playerPosition.X - Position.X;
        if (_attackRemaining > 0f)
        {
            _attackRemaining = Math.Max(0f, _attackRemaining - elapsedSeconds);
            _facing = horizontalDistance < 0f ? -1 : 1;
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else if (Math.Abs(horizontalDistance) <= AttackDistance && _attackCooldown <= 0f)
        {
            _attackRemaining = 0.55f;
            _attackCooldown = 1.4f;
            _attackHitApplied = false;
            _facing = horizontalDistance < 0f ? -1 : 1;
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else
        {
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

    public bool TryConsumeAttackHit(Rectangle playerHitbox)
    {
        if (!IsAttacking || _attackHitApplied || !AttackHitbox.Intersects(playerHitbox))
            return false;

        _attackHitApplied = true;
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
        Character.Draw(spriteBatch, Position, EnemyScale, effects);
    }
}
