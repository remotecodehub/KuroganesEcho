using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

/// <summary>
/// Simple reusable Act enemy instance: patrols its Tiled spawn area and plays
/// attack animations when the player approaches.
/// </summary>
internal sealed class EnemyInstance
{
    private const float PatrolHalfWidth = 72f;
    private const float AttackDistance = 78f;
    private const float EnemyScale = 0.5f;

    private readonly float _patrolLeft;
    private readonly float _patrolRight;
    private float _attackRemaining;
    private float _attackCooldown;
    private int _facing = 1;

    public EnemyInstance(EnemyCharacter character, LevelObjectData spawn)
    {
        Character = character;
        Spawn = spawn;
        Position = new Vector2(spawn.Bounds.Center.X, spawn.Bounds.Bottom);
        _patrolLeft = Position.X - PatrolHalfWidth;
        _patrolRight = Position.X + PatrolHalfWidth;
    }

    public EnemyCharacter Character { get; }
    public LevelObjectData Spawn { get; }
    public Vector2 Position { get; private set; }

    public void Reset()
    {
        Position = new Vector2(Spawn.Bounds.Center.X, Spawn.Bounds.Bottom);
        _attackRemaining = 0f;
        _attackCooldown = 0f;
        _facing = 1;
        Character.Reset();
    }

    public void Update(float elapsedSeconds, Vector2 playerPosition, GameLevel level)
    {
        _attackCooldown = Math.Max(0f, _attackCooldown - elapsedSeconds);

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

        var landingTop = level.FindLandingTop(Position.X, Position.Y - 2f, Position.Y + 2f);
        if (landingTop.HasValue)
            Position = new Vector2(Position.X, landingTop.Value);

        Character.Update(elapsedSeconds);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var effects = _facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Character.Draw(spriteBatch, Position, EnemyScale, effects);
    }
}
