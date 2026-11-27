using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal sealed class EnemyProjectile
{
    private const float LifetimeSeconds = 2f;
    private readonly float _speed;
    private readonly float _halfWidth;
    private readonly float _halfHeight;
    private readonly Texture2D _texture;
    private readonly int _frameCount;
    private readonly float _scale;
    private float _animationClock;
    private float _remaining = LifetimeSeconds;

    public EnemyProjectile(EnemyCharacter character, Vector2 position, Vector2 direction)
    {
        _texture = character.RangedProjectileTexture;
        _frameCount = character.RangedProjectileFrameCount;
        (_speed, _halfWidth, _halfHeight, _scale) = character.RangedAttackKind switch
        {
            EnemyRangedAttackKind.Cast => (180f, 7f, 7f, 0.65f),
            EnemyRangedAttackKind.Shot => (260f, 8f, 3f, 0.55f),
            _ => throw new ArgumentOutOfRangeException(nameof(character), "Enemy has no ranged attack")
        };
        Damage = CombatBalance.EnemyAttackDamage;
        ImpactCue = AudioCue.Magic;
        ImpactEffect = CombatEffectKind.Magic;
        Position = position;
        Direction = direction.LengthSquared() > 0.001f
            ? Vector2.Normalize(direction)
            : Vector2.UnitX;
        SourceX = position.X;
    }

    public EnemyProjectile(
        Texture2D texture,
        int frameCount,
        Vector2 position,
        Vector2 direction,
        float speed,
        float halfWidth,
        float halfHeight,
        float scale,
        int damage,
        AudioCue impactCue = AudioCue.Magic,
        CombatEffectKind impactEffect = CombatEffectKind.Magic,
        float lifetimeSeconds = LifetimeSeconds)
    {
        _texture = texture;
        _frameCount = Math.Max(1, frameCount);
        _speed = speed;
        _halfWidth = halfWidth;
        _halfHeight = halfHeight;
        _scale = scale;
        _remaining = lifetimeSeconds;
        Damage = damage;
        ImpactCue = impactCue;
        ImpactEffect = impactEffect;
        Position = position;
        Direction = direction.LengthSquared() > 0.001f
            ? Vector2.Normalize(direction)
            : Vector2.UnitX;
        SourceX = position.X;
    }

    public Vector2 Position { get; private set; }
    public Vector2 Direction { get; }
    public float SourceX { get; }
    public int Damage { get; }
    public AudioCue ImpactCue { get; }
    public CombatEffectKind ImpactEffect { get; }
    public bool IsExpired => _remaining <= 0f;
    public Rectangle Hitbox => new(
        (int)(Position.X - _halfWidth),
        (int)(Position.Y - _halfHeight),
        (int)(_halfWidth * 2f),
        (int)(_halfHeight * 2f));

    public void Update(float elapsedSeconds, Rectangle worldBounds)
    {
        Position += Direction * _speed * elapsedSeconds;
        _animationClock += elapsedSeconds;
        _remaining -= elapsedSeconds;
        if (!worldBounds.Intersects(Hitbox))
            _remaining = 0f;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var rotation = (float)Math.Atan2(Direction.Y, Direction.X);
        var frameWidth = _texture.Width / _frameCount;
        var frame = (int)(_animationClock * 14f) % _frameCount;
        var source = new Rectangle(frame * frameWidth, 0, frameWidth, _texture.Height);
        spriteBatch.Draw(
            _texture,
            Position,
            source,
            Color.White,
            rotation,
            new Vector2(frameWidth / 2f, _texture.Height / 2f),
            _scale,
            SpriteEffects.None,
            0f);
    }
}
