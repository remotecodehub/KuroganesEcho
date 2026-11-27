using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal enum PlayerProjectileKind
{
    Spine,
    Kunai,
    Dart
}

/// <summary>Small, screen-independent projectile used by the players' special attacks.</summary>
internal sealed class PlayerProjectile
{
    private readonly Rectangle _source;
    private readonly Vector2 _hitboxSize;
    private float _remaining = 1.6f;

    public PlayerProjectile(
        Texture2D texture,
        Rectangle source,
        Vector2 position,
        int direction,
        PlayerProjectileKind kind)
    {
        Texture = texture;
        _source = source;
        Position = position;
        Direction = direction < 0 ? -1 : 1;
        Kind = kind;
        Speed = kind switch
        {
            PlayerProjectileKind.Spine => 250f,
            PlayerProjectileKind.Kunai => 235f,
            _ => 270f
        };
        Scale = kind switch
        {
            PlayerProjectileKind.Spine => 1.4f,
            PlayerProjectileKind.Kunai => 1.25f,
            _ => 1.7f
        };
        _hitboxSize = kind switch
        {
            PlayerProjectileKind.Spine => new Vector2(14f, 10f),
            PlayerProjectileKind.Kunai => new Vector2(17f, 8f),
            _ => new Vector2(12f, 8f)
        };
    }

    public Texture2D Texture { get; }
    public Vector2 Position { get; private set; }
    public int Direction { get; }
    public float Speed { get; }
    public float Scale { get; }
    public PlayerProjectileKind Kind { get; }
    public bool IsExpired => _remaining <= 0f;

    public Rectangle Hitbox => new(
        (int)(Position.X - _hitboxSize.X / 2f),
        (int)(Position.Y - _hitboxSize.Y / 2f),
        (int)_hitboxSize.X,
        (int)_hitboxSize.Y);

    public void Update(float elapsedSeconds, Rectangle worldBounds)
    {
        Position += new Vector2(Direction * Speed * elapsedSeconds, 0f);
        _remaining -= elapsedSeconds;
        if (Position.X < worldBounds.Left - 32f || Position.X > worldBounds.Right + 32f)
            _remaining = 0f;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var effects = Direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        var origin = new Vector2(_source.Width / 2f, _source.Height / 2f);
        spriteBatch.Draw(Texture, Position, _source, Color.White, 0f, origin, Scale, effects, 0f);
    }
}
