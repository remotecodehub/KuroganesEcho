using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal enum CombatEffectKind
{
    Slash,
    Magic,
    Impact,
    Dust,
    Death
}

/// <summary>
/// Small, asset-independent effect renderer used by every player, regular
/// enemy and boss. It keeps effects deterministic and visible even when a
/// character tileset only contains an animation sheet and no projectile art.
/// </summary>
internal sealed class CombatEffectSystem
{
    private sealed class Effect
    {
        public Vector2 Position;
        public Vector2 Direction;
        public CombatEffectKind Kind;
        public float Age;
        public float Duration;
        public float Scale;
    }

    private readonly List<Effect> _effects = new();

    public void Spawn(Vector2 position, CombatEffectKind kind, Vector2 direction, float scale = 1f)
    {
        _effects.Add(new Effect
        {
            Position = position,
            Direction = direction.LengthSquared() > 0.001f ? Vector2.Normalize(direction) : Vector2.UnitX,
            Kind = kind,
            Duration = kind switch
            {
                CombatEffectKind.Death => 0.55f,
                CombatEffectKind.Magic => 0.42f,
                _ => 0.28f
            },
            Scale = scale
        });
    }

    public void Update(float elapsedSeconds)
    {
        for (var index = _effects.Count - 1; index >= 0; index--)
        {
            var effect = _effects[index];
            effect.Age += elapsedSeconds;
            effect.Position += effect.Direction * elapsedSeconds * (effect.Kind == CombatEffectKind.Dust ? 6f : 12f);
            if (effect.Age >= effect.Duration)
                _effects.RemoveAt(index);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        foreach (var effect in _effects)
        {
            var progress = Math.Clamp(effect.Age / effect.Duration, 0f, 1f);
            var fade = 1f - progress;
            var radius = (4f + progress * 10f) * effect.Scale;
            var color = effect.Kind switch
            {
                CombatEffectKind.Slash => new Color(238, 220, 142) * fade,
                CombatEffectKind.Magic => new Color(116, 178, 255) * fade,
                CombatEffectKind.Impact => new Color(255, 138, 88) * fade,
                CombatEffectKind.Dust => new Color(184, 169, 137) * fade,
                _ => new Color(214, 86, 76) * fade
            };

            var center = effect.Position;
            var horizontal = new Rectangle((int)(center.X - radius), (int)(center.Y - 1f), (int)(radius * 2f), 2);
            var vertical = new Rectangle((int)(center.X - 1f), (int)(center.Y - radius), 2, (int)(radius * 2f));
            spriteBatch.Draw(pixel, horizontal, color);
            spriteBatch.Draw(pixel, vertical, color);

            if (effect.Kind is CombatEffectKind.Slash or CombatEffectKind.Magic)
            {
                var diagonal = new Vector2(-effect.Direction.Y, effect.Direction.X) * radius;
                DrawLine(spriteBatch, pixel, center - diagonal, center + diagonal, 1f, color);
            }
        }
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, float thickness, Color color)
    {
        var delta = to - from;
        var length = delta.Length();
        var angle = (float)Math.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, from, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }
}
