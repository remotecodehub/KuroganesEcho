using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace KuroganesEcho.Core;

internal enum EnemyAnimationState
{
    Idle,
    Walk,
    Run,
    Attack1,
    Attack2,
    Attack3,
    Hurt,
    Dead,
    Jump,
    Special1,
    Special2
}

/// <summary>
/// Loads and animates an enemy sprite-sheet set. Enemy movement and behavior
/// remain outside this class so the same presentation can be reused by every act.
/// </summary>
internal sealed class EnemyCharacter
{
    private sealed record AnimationClip(Texture2D Texture, int FrameCount);

    private readonly Dictionary<EnemyAnimationState, AnimationClip> _clips = new();
    private float _animationClock;

    private const float DeadFrameRate = 3f;

    private EnemyCharacter(string id)
    {
        Id = id;
        Animation = EnemyAnimationState.Idle;
    }

    public string Id { get; }
    public EnemyAnimationState Animation { get; private set; }

    public static EnemyCharacter Load(ContentManager content, string enemyType)
    {
        var enemy = new EnemyCharacter(enemyType);
        switch (enemyType)
        {
            case "Black_Werewolf":
                enemy.LoadWerewolf(content);
                break;
            case "Kitsune":
                enemy.LoadKitsune(content);
                break;
            case "Skeleton_Warrior":
                enemy.LoadSkeleton(content);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(enemyType), enemyType, "Unknown enemy type");
        }

        return enemy;
    }

    public void SetAnimation(EnemyAnimationState animation)
    {
        if (!_clips.ContainsKey(animation))
            animation = EnemyAnimationState.Idle;

        if (Animation != animation)
        {
            Animation = animation;
            _animationClock = 0f;
        }
    }

    public void Update(float elapsedSeconds)
    {
        _animationClock += elapsedSeconds;
    }

    public void Reset()
    {
        Animation = EnemyAnimationState.Idle;
        _animationClock = 0f;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position, float scale, SpriteEffects effects)
    {
        var clip = _clips[Animation];
        var frameWidth = clip.Texture.Width / clip.FrameCount;
        var frameRate = Animation == EnemyAnimationState.Dead
            ? DeadFrameRate
            : Animation == EnemyAnimationState.Idle ? 7f : 10f;
        var frame = Animation == EnemyAnimationState.Dead
            ? Math.Min((int)(_animationClock * frameRate), clip.FrameCount - 1)
            : (int)(_animationClock * frameRate) % clip.FrameCount;
        var source = new Rectangle(frame * frameWidth, 0, frameWidth, clip.Texture.Height);
        var origin = new Vector2(frameWidth / 2f, clip.Texture.Height);
        spriteBatch.Draw(clip.Texture, position, source, Color.White, 0f, origin, scale, effects, 0f);
    }

    private void LoadWerewolf(ContentManager content)
    {
        const string root = "Tiled/Enemies/craftpix-net-248468-free-werewolf-sprite-sheets-pixel-art/Black_Werewolf";
        Add(content, EnemyAnimationState.Idle, root, "Idle", 8);
        Add(content, EnemyAnimationState.Walk, root, "walk", 11);
        Add(content, EnemyAnimationState.Run, root, "Run", 9);
        Add(content, EnemyAnimationState.Attack1, root, "Attack_1", 6);
        Add(content, EnemyAnimationState.Attack2, root, "Attack_2", 4);
        Add(content, EnemyAnimationState.Attack3, root, "Attack_3", 5);
        Add(content, EnemyAnimationState.Hurt, root, "Hurt", 2);
        Add(content, EnemyAnimationState.Dead, root, "Dead", 2);
        Add(content, EnemyAnimationState.Jump, root, "Jump", 11);
    }

    private void LoadKitsune(ContentManager content)
    {
        const string root = "Tiled/Enemies/craftpix-net-605776-free-yokai-pixel-art-character-sprites/Kitsune";
        Add(content, EnemyAnimationState.Idle, root, "Idle", 8);
        Add(content, EnemyAnimationState.Walk, root, "Walk", 8);
        Add(content, EnemyAnimationState.Run, root, "Run", 8);
        Add(content, EnemyAnimationState.Attack1, root, "Attack_1", 10);
        Add(content, EnemyAnimationState.Attack2, root, "Attack_2", 10);
        Add(content, EnemyAnimationState.Attack3, root, "Attack_3", 7);
        Add(content, EnemyAnimationState.Hurt, root, "Hurt", 2);
        Add(content, EnemyAnimationState.Dead, root, "Dead", 10);
        Add(content, EnemyAnimationState.Jump, root, "Jump", 10);
        Add(content, EnemyAnimationState.Special1, root, "Fire_1", 14);
        Add(content, EnemyAnimationState.Special2, root, "Fire_2", 11);
    }

    private void LoadSkeleton(ContentManager content)
    {
        const string root = "Tiled/Enemies/craftpix-net-957123-free-skeleton-pixel-art-sprite-sheets/Skeleton_Warrior";
        Add(content, EnemyAnimationState.Idle, root, "Idle", 7);
        Add(content, EnemyAnimationState.Walk, root, "Walk", 7);
        Add(content, EnemyAnimationState.Run, root, "Run", 8);
        Add(content, EnemyAnimationState.Attack1, root, "Attack_1", 5);
        Add(content, EnemyAnimationState.Attack2, root, "Attack_2", 6);
        Add(content, EnemyAnimationState.Attack3, root, "Attack_3", 4);
        Add(content, EnemyAnimationState.Hurt, root, "Hurt", 2);
        Add(content, EnemyAnimationState.Dead, root, "Dead", 4);
        Add(content, EnemyAnimationState.Special1, root, "Protect", 1);
        Add(content, EnemyAnimationState.Special2, root, "Run+attack", 7);
    }

    private void Add(ContentManager content, EnemyAnimationState state, string root, string assetName, int frameCount)
    {
        _clips[state] = new AnimationClip(content.Load<Texture2D>($"{root}/{assetName}"), frameCount);
    }
}
