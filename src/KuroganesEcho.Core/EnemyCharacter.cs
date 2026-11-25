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
    private sealed record EnemyDefinition(
        string Root,
        string Idle,
        string Walk,
        string Attack1,
        string Hurt,
        string Dead,
        string Run = null,
        string Attack2 = null,
        string Attack3 = null,
        string Jump = null,
        string Special1 = null,
        string Special2 = null);

    private static readonly IReadOnlyDictionary<string, EnemyDefinition> Definitions =
        new Dictionary<string, EnemyDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Black_Werewolf"] = new(
                "Tiled/Enemies/craftpix-net-248468-free-werewolf-sprite-sheets-pixel-art/Black_Werewolf",
                "Idle", "walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump"),
            ["Red_Werewolf"] = new(
                "Tiled/Enemies/craftpix-net-248468-free-werewolf-sprite-sheets-pixel-art/Red_Werewolf",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump"),
            ["White_Werewolf"] = new(
                "Tiled/Enemies/craftpix-net-248468-free-werewolf-sprite-sheets-pixel-art/White_Werewolf",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump"),
            ["Karasu_tengu"] = new(
                "Tiled/Enemies/craftpix-net-605776-free-yokai-pixel-art-character-sprites/Karasu_tengu",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump"),
            ["Kitsune"] = new(
                "Tiled/Enemies/craftpix-net-605776-free-yokai-pixel-art-character-sprites/Kitsune",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump", "Fire_1", "Fire_2"),
            ["Yamabushi_tengu"] = new(
                "Tiled/Enemies/craftpix-net-605776-free-yokai-pixel-art-character-sprites/Yamabushi_tengu",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", "Jump"),
            ["Skeleton_Archer"] = new(
                "Tiled/Enemies/craftpix-net-957123-free-skeleton-pixel-art-sprite-sheets/Skeleton_Archer",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", null, "Attack_2", "Attack_3", null, "Shot_1", "Shot_2"),
            ["Skeleton_Spearman"] = new(
                "Tiled/Enemies/craftpix-net-957123-free-skeleton-pixel-art-sprite-sheets/Skeleton_Spearman",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", null, null, "Protect", "Run+attack"),
            ["Skeleton_Warrior"] = new(
                "Tiled/Enemies/craftpix-net-957123-free-skeleton-pixel-art-sprite-sheets/Skeleton_Warrior",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", null, "Protect", "Run+attack"),
            ["Minotaur_1"] = new(
                "Tiled/Enemies/craftpix-net-170637-free-minotaur-sprite-sheet-pixel-art-pack/Minotaur_1",
                "Idle", "Walk", "Attack", "Hurt", "Dead"),
            ["Minotaur_2"] = new(
                "Tiled/Enemies/craftpix-net-170637-free-minotaur-sprite-sheet-pixel-art-pack/Minotaur_2",
                "Idle", "Walk", "Attack", "Hurt", "Dead"),
            ["Minotaur_3"] = new(
                "Tiled/Enemies/craftpix-net-170637-free-minotaur-sprite-sheet-pixel-art-pack/Minotaur_3",
                "Idle", "Walk", "Attack", "Hurt", "Dead"),
            ["Gorgon_1"] = new(
                "Tiled/Enemies/craftpix-net-280097-free-gorgon-pixel-art-character-sprite-sheets/Gorgon_1",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", null, "Special"),
            ["Gorgon_2"] = new(
                "Tiled/Enemies/craftpix-net-280097-free-gorgon-pixel-art-character-sprite-sheets/Gorgon_2",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", null, "Special"),
            ["Gorgon_3"] = new(
                "Tiled/Enemies/craftpix-net-280097-free-gorgon-pixel-art-character-sprite-sheets/Gorgon_3",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", "Attack_3", null, "Special"),
            ["Fire Wizard"] = new(
                "Tiled/Enemies/craftpix-net-529677-free-wizard-sprite-sheets-pixel-art/Fire Wizard",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", null, "Jump", "Fireball", "Flame_jet"),
            ["Lightning Mage"] = new(
                "Tiled/Enemies/craftpix-net-529677-free-wizard-sprite-sheets-pixel-art/Lightning Mage",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", null, "Jump", "Light_ball", "Light_charge"),
            ["Wanderer Magican"] = new(
                "Tiled/Enemies/craftpix-net-529677-free-wizard-sprite-sheets-pixel-art/Wanderer Magican",
                "Idle", "Walk", "Attack_1", "Hurt", "Dead", "Run", "Attack_2", null, "Jump", "Charge_1", "Charge_2")
        };

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
        if (!Definitions.TryGetValue(enemyType, out var definition))
            throw new ArgumentOutOfRangeException(nameof(enemyType), enemyType, "Unknown enemy type");

        var enemy = new EnemyCharacter(enemyType);
        enemy.LoadDefinition(content, definition);
        return enemy;
    }

    public static bool IsSupported(string enemyType) => Definitions.ContainsKey(enemyType);

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

    private void LoadDefinition(ContentManager content, EnemyDefinition definition)
    {
        Add(content, EnemyAnimationState.Idle, definition.Root, definition.Idle);
        Add(content, EnemyAnimationState.Walk, definition.Root, definition.Walk);
        Add(content, EnemyAnimationState.Attack1, definition.Root, definition.Attack1);
        Add(content, EnemyAnimationState.Hurt, definition.Root, definition.Hurt);
        Add(content, EnemyAnimationState.Dead, definition.Root, definition.Dead);
        AddOptional(content, EnemyAnimationState.Run, definition.Root, definition.Run);
        AddOptional(content, EnemyAnimationState.Attack2, definition.Root, definition.Attack2);
        AddOptional(content, EnemyAnimationState.Attack3, definition.Root, definition.Attack3);
        AddOptional(content, EnemyAnimationState.Jump, definition.Root, definition.Jump);
        AddOptional(content, EnemyAnimationState.Special1, definition.Root, definition.Special1);
        AddOptional(content, EnemyAnimationState.Special2, definition.Root, definition.Special2);
    }

    private void Add(ContentManager content, EnemyAnimationState state, string root, string assetName)
    {
        var texture = content.Load<Texture2D>($"{root}/{assetName}");
        _clips[state] = new AnimationClip(texture, GetFrameCount(texture));
    }

    private void AddOptional(ContentManager content, EnemyAnimationState state, string root, string assetName)
    {
        if (assetName is not null)
            Add(content, state, root, assetName);
    }

    private static int GetFrameCount(Texture2D texture) =>
        texture.Height > 0 && texture.Width % texture.Height == 0
            ? Math.Max(1, texture.Width / texture.Height)
            : 1;

    public static bool IsBossType(string enemyType) =>
        enemyType is "Minotaur_1" or "Minotaur_2" or "Minotaur_3" or
            "Gorgon_1" or "Gorgon_2" or "Gorgon_3" or
            "Fire Wizard" or "Lightning Mage" or "Wanderer Magican";

    public static string DefaultBossVariant(string bossType) => bossType switch
    {
        "Minotaur" => "Minotaur_1",
        "Gorgon" => "Gorgon_1",
        "Wizard" => "Fire Wizard",
        _ => throw new ArgumentOutOfRangeException(nameof(bossType), bossType, "Unknown boss type")
    };
}
