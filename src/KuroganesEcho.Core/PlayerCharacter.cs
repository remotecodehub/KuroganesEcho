using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal enum PlayerAnimationState
{
    Idle,
    Run,
    Attack1,
    Attack2,
    Cast,
    Walk,
    Eating,
    Jump,
    Dead
}

/// <summary>
/// Reusable player presentation and animation model. The level owns physics and
/// input; this class owns the selected character's sprite sheets and animation.
/// </summary>
internal sealed class PlayerCharacter
{
    private readonly Dictionary<PlayerAnimationState, Texture2D> _textures = new();
    private readonly Dictionary<PlayerAnimationState, int> _frameCounts = new();
    private float _animationClock;
    private int _lastStepFrame = -1;
    private bool _footstepPending;

    private const float DeadFrameRate = 3f;

    private PlayerCharacter(string id)
    {
        Id = id;
        Animation = PlayerAnimationState.Idle;
    }

    public string Id { get; }

    public PlayerAnimationState Animation { get; private set; }

    public float ActionRemaining { get; private set; }

    public bool IsActionActive => ActionRemaining > 0f;

    public static PlayerCharacter Load(ContentManager content, string characterId)
    {
        var player = new PlayerCharacter(characterId);
        var root = $"Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/{characterId}";
        var isKunoichi = string.Equals(characterId, "Kunoichi", StringComparison.Ordinal);
        var hasCast = isKunoichi || string.Equals(characterId, "Ninja_Monk", StringComparison.Ordinal);

        player.Add(content, PlayerAnimationState.Idle, $"{root}/Idle", FrameCount(characterId, PlayerAnimationState.Idle));
        player.Add(content, PlayerAnimationState.Run, $"{root}/Run", FrameCount(characterId, PlayerAnimationState.Run));
        player.Add(content, PlayerAnimationState.Walk, $"{root}/Walk", FrameCount(characterId, PlayerAnimationState.Walk));
        player.Add(content, PlayerAnimationState.Attack1, $"{root}/Attack_1", FrameCount(characterId, PlayerAnimationState.Attack1));
        player.Add(content, PlayerAnimationState.Attack2, $"{root}/Attack_2", FrameCount(characterId, PlayerAnimationState.Attack2));
        player.Add(content, PlayerAnimationState.Jump, $"{root}/Jump", FrameCount(characterId, PlayerAnimationState.Jump));
        player.Add(content, PlayerAnimationState.Dead, $"{root}/Dead", FrameCount(characterId, PlayerAnimationState.Dead));

        if (hasCast)
            player.Add(content, PlayerAnimationState.Cast, $"{root}/Cast", FrameCount(characterId, PlayerAnimationState.Cast));
        else
            player.UseFallback(PlayerAnimationState.Cast, PlayerAnimationState.Attack1);

        if (isKunoichi)
            player.Add(content, PlayerAnimationState.Eating, $"{root}/Eating", FrameCount(characterId, PlayerAnimationState.Eating));
        else
            player.UseFallback(PlayerAnimationState.Eating, PlayerAnimationState.Idle);

        return player;
    }

    public void SetAction(PlayerAnimationState animation)
    {
        Animation = animation;
        ActionRemaining = animation == PlayerAnimationState.Jump ? 0.9f : 0.65f;
        _animationClock = 0f;
        _lastStepFrame = -1;
    }

    public void SetDead()
    {
        if (Animation == PlayerAnimationState.Dead)
            return;

        Animation = PlayerAnimationState.Dead;
        ActionRemaining = FrameCountForAnimation(PlayerAnimationState.Dead) / DeadFrameRate;
        _animationClock = 0f;
        _lastStepFrame = -1;
        _footstepPending = false;
    }

    public bool IsDeadAnimationComplete =>
        Animation == PlayerAnimationState.Dead &&
        _animationClock >= _frameCounts[PlayerAnimationState.Dead] / DeadFrameRate;

    public void SetLocomotion(PlayerAnimationState animation)
    {
        if (IsActionActive)
            return;

        if (Animation != animation)
        {
            _animationClock = 0f;
            _lastStepFrame = -1;
        }
        Animation = animation;
    }

    public void Reset()
    {
        ActionRemaining = 0f;
        Animation = PlayerAnimationState.Idle;
        _animationClock = 0f;
        _lastStepFrame = -1;
        _footstepPending = false;
    }

    /// <summary>True uma vez por passo, quando o pé toca o chão ao andar ou correr.</summary>
    public bool ConsumeFootstep()
    {
        var pending = _footstepPending;
        _footstepPending = false;
        return pending;
    }

    public void Update(float elapsedSeconds)
    {
        _animationClock += elapsedSeconds;
        UpdateFootstep();
        if (ActionRemaining > 0f)
            ActionRemaining = Math.Max(0f, ActionRemaining - elapsedSeconds);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position, float scale, SpriteEffects effects)
    {
        var texture = _textures[Animation];
        var frameCount = _frameCounts[Animation];
        var frameWidth = texture.Width / frameCount;
        var frameRate = Animation == PlayerAnimationState.Dead
            ? DeadFrameRate
            : Animation == PlayerAnimationState.Idle ? 7f : 10f;
        var frame = Animation == PlayerAnimationState.Dead
            ? Math.Min((int)(_animationClock * frameRate), frameCount - 1)
            : (int)(_animationClock * frameRate) % frameCount;
        var source = new Rectangle(frame * frameWidth, 0, frameWidth, texture.Height);
        var origin = new Vector2(frameWidth / 2f, texture.Height);
        spriteBatch.Draw(texture, position, source, Color.White, 0f, origin, scale, effects, 0f);
    }

    private void UpdateFootstep()
    {
        if (Animation is not (PlayerAnimationState.Walk or PlayerAnimationState.Run))
        {
            _lastStepFrame = -1;
            return;
        }

        var frameCount = _frameCounts[Animation];
        var frame = (int)(_animationClock * 10f) % frameCount;
        if (frame == _lastStepFrame)
            return;

        _lastStepFrame = frame;
        // Dois passos por ciclo: no frame inicial e no meio do ciclo.
        if (frame == 0 || frame == frameCount / 2)
            _footstepPending = true;
    }

    private void Add(ContentManager content, PlayerAnimationState animation, string asset, int frameCount)
    {
        _textures[animation] = content.Load<Texture2D>(asset);
        _frameCounts[animation] = frameCount;
    }

    private void UseFallback(PlayerAnimationState animation, PlayerAnimationState fallback)
    {
        _textures[animation] = _textures[fallback];
        _frameCounts[animation] = _frameCounts[fallback];
    }

    private static int FrameCount(string characterId, PlayerAnimationState animation) => characterId switch
    {
        "Kunoichi" => animation switch
        {
            PlayerAnimationState.Idle or PlayerAnimationState.Eating => 9,
            PlayerAnimationState.Run or PlayerAnimationState.Walk => 8,
            PlayerAnimationState.Attack1 => 6,
            PlayerAnimationState.Attack2 => 8,
            PlayerAnimationState.Cast => 6,
            PlayerAnimationState.Jump => 10,
            PlayerAnimationState.Dead => 5,
            _ => 1
        },
        "Ninja_Monk" => animation switch
        {
            PlayerAnimationState.Idle or PlayerAnimationState.Walk => 7,
            PlayerAnimationState.Run or PlayerAnimationState.Attack1 or PlayerAnimationState.Attack2 or PlayerAnimationState.Cast => 5,
            PlayerAnimationState.Jump => 9,
            PlayerAnimationState.Dead => 5,
            _ => 1
        },
        "Ninja_Peasant" => animation switch
        {
            PlayerAnimationState.Idle => 6,
            PlayerAnimationState.Run => 6,
            PlayerAnimationState.Walk => 8,
            PlayerAnimationState.Attack1 => 6,
            PlayerAnimationState.Attack2 => 4,
            PlayerAnimationState.Jump => 8,
            PlayerAnimationState.Dead => 4,
            _ => 1
        },
        _ => throw new ArgumentOutOfRangeException(nameof(characterId), characterId, "Unknown player character")
    };

    private int FrameCountForAnimation(PlayerAnimationState animation) => _frameCounts[animation];
}
