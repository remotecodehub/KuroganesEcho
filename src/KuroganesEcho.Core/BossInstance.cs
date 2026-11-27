using System;
using System.Collections.Generic;
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
    private const float MinotaurScale = 0.85f;
    private const float FireWizardScale = 1.20f;
    private const float LightningMageScale = 1.25f;
    private const float WandererMagicanScale = 1.20f;
    private const float MinotaurAttackDistance = 112f;
    private const float MinotaurRetreatDistance = 72f;
    private const float MinotaurAdvanceDistance = 96f;
    private const float WizardMeleeDistance = 64f;
    private const float WizardFlameJetDistance = 96f;
    private const float WizardRangedDistance = 260f;
    private const float AttackDuration = 0.8f;
    private const float AttackCooldown = 1.6f;
    private const float WizardAttackCooldownMultiplier = 1.5f;
    private const int HitsToStun = 5;
    private const int WizardHitsToBreakStun = 2;
    private const float BossHalfWidth = 16f;
    private const float BossHeight = 48f;
    private const float ArenaHalfWidth = 68f;
    private const float Gravity = 480f;

    private readonly float _groundY;
    private readonly float _arenaLeft;
    private readonly float _arenaRight;
    private float _attackRemaining;
    private float _attackCooldown;
    private float _jumpCooldown;
    private float _verticalVelocity;
    private bool _attackHitApplied;
    private bool _rangedAttack;
    private bool _rangedReleased;
    private EnemyAnimationState _activeAttackAnimation = EnemyAnimationState.Attack1;
    private int _attackPatternIndex;
    private int _facing = 1;
    private int _hitsSinceLastStun;
    private int _hitsWhileStunned;
    private bool _isStunned;
    private bool _minotaurAdvancing = true;
    private GameDifficulty _difficulty = GameDifficulty.Normal;
    private bool _recoveryApplied;

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
        _groundY = Position.Y;
        _arenaLeft = Position.X - ArenaHalfWidth;
        _arenaRight = Position.X + ArenaHalfWidth;
        Health = MaxHealth;
    }

    public BossKind Kind { get; }
    public string VariantId { get; }
    public LevelObjectData Spawn { get; }
    public EnemyCharacter Character { get; }
    public bool LocksArena =>
        Spawn.Properties.TryGetValue("LockArena", out var lockArena) &&
        bool.TryParse(lockArena, out var enabled) &&
        enabled;
    public Vector2 Position { get; private set; }
    public int MaxHealth => CombatBalance.GetBossMaxHealth(_difficulty);
    public int Health { get; private set; }
    public bool IsAlive => Health > 0;
    public bool IsStunned => _isStunned;
    public bool RecoveryApplied => _recoveryApplied;
    public bool IsDeathAnimationComplete => !IsAlive && Character.IsDeadAnimationComplete;
    public bool IsEnraged => IsAlive && Health <= MaxHealth / 2;
    public bool IsAttacking => IsAlive && _attackRemaining > 0f;
    public float VisualScale => GetBossScale(VariantId);

    public string LocalizationNameKey => VariantId switch
    {
        "Minotaur_1" => "BossMinotaur1",
        "Minotaur_2" => "BossMinotaur2",
        "Minotaur_3" => "BossMinotaur3",
        "Fire Wizard" => "BossFireWizard",
        "Lightning Mage" => "BossLightningMage",
        "Wanderer Magican" => "BossWandererMagican",
        _ => "BossMinotaur1"
    };

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

    private Rectangle AttackHitbox
    {
        get
        {
            var isFlameJet = VariantId == "Fire Wizard" && _activeAttackAnimation == EnemyAnimationState.Special2;
            var width = isFlameJet ? 84 : 48;
            var left = _facing > 0 ? Position.X + 6f : Position.X - 6f - width;
            return new Rectangle((int)left, (int)(Position.Y - 40f), width, 30);
        }
    }

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
        Position = new Vector2(Spawn.Bounds.Center.X, _groundY);
        Health = MaxHealth;
        _attackRemaining = 0f;
        _attackCooldown = 0f;
        _jumpCooldown = 0f;
        _verticalVelocity = 0f;
        _attackHitApplied = false;
        _rangedAttack = false;
        _rangedReleased = false;
        _activeAttackAnimation = EnemyAnimationState.Attack1;
        _attackPatternIndex = 0;
        _facing = 1;
        _hitsSinceLastStun = 0;
        _hitsWhileStunned = 0;
        _isStunned = false;
        _minotaurAdvancing = true;
        _recoveryApplied = false;
        Character.Reset();
    }

    public void Update(float elapsedSeconds, Vector2 playerPosition)
    {
        _attackCooldown = Math.Max(0f, _attackCooldown - elapsedSeconds);
        _jumpCooldown = Math.Max(0f, _jumpCooldown - elapsedSeconds);
        if (!IsAlive)
        {
            ApplyGravity(elapsedSeconds);
            Character.Update(elapsedSeconds);
            return;
        }

        if (IsStunned)
        {
            _attackRemaining = 0f;
            _rangedAttack = false;
            _rangedReleased = false;
            Character.SetAnimation(EnemyAnimationState.Hurt);
            ApplyGravity(elapsedSeconds);
            Character.Update(elapsedSeconds);
            return;
        }

        if (Kind == BossKind.Minotaur)
        {
            UpdateMinotaur(elapsedSeconds, playerPosition);
        }
        else
        {
            UpdateWizard(elapsedSeconds, playerPosition);
        }

        ApplyGravity(elapsedSeconds);
        Character.Update(elapsedSeconds);
    }

    private void UpdateMinotaur(float elapsedSeconds, Vector2 playerPosition)
    {
        var horizontalDistance = playerPosition.X - Position.X;
        var absDistance = Math.Abs(horizontalDistance);
        _facing = horizontalDistance < 0f ? -1 : 1;
        if (_attackRemaining > 0f)
        {
            _attackRemaining = Math.Max(0f, _attackRemaining - elapsedSeconds);
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else if (absDistance <= MinotaurAttackDistance && _attackCooldown <= 0f)
        {
            _attackRemaining = AttackDuration;
            _attackCooldown = IsEnraged ? AttackCooldown * 0.8f : AttackCooldown;
            _attackHitApplied = false;
            _rangedAttack = false;
            _activeAttackAnimation = EnemyAnimationState.Attack1;
            Character.SetAnimation(EnemyAnimationState.Attack1);
        }
        else
        {
            if (absDistance >= MinotaurAdvanceDistance)
                _minotaurAdvancing = true;
            else if (absDistance <= MinotaurRetreatDistance)
                _minotaurAdvancing = false;

            var walkSpeed = IsEnraged ? 45f : 34f;
            var movementDirection = _facing * (_minotaurAdvancing ? 1 : -1);
            var nextX = Math.Clamp(Position.X + movementDirection * walkSpeed * elapsedSeconds, _arenaLeft, _arenaRight);
            var moved = Math.Abs(nextX - Position.X) > 0.05f;
            Position = new Vector2(nextX, Position.Y);
            Character.SetAnimation(moved ? EnemyAnimationState.Walk : EnemyAnimationState.Idle);
        }
    }

    private void UpdateWizard(float elapsedSeconds, Vector2 playerPosition)
    {
        var horizontalDistance = playerPosition.X - Position.X;
        var absDistance = Math.Abs(horizontalDistance);
        _facing = horizontalDistance < 0f ? -1 : 1;

        if (_attackRemaining > 0f)
        {
            _attackRemaining = Math.Max(0f, _attackRemaining - elapsedSeconds);
            Character.SetAnimation(_activeAttackAnimation);
            return;
        }

        if (_attackCooldown <= 0f && absDistance <= WizardRangedDistance)
        {
            StartWizardAttack(absDistance);
            return;
        }

        // Arena repositioning between attacks
        var desiredDistance = VariantId switch
        {
            "Fire Wizard" => 92f,
            "Lightning Mage" => 118f,
            _ => 104f
        };

        var moveSpeed = VariantId switch
        {
            "Lightning Mage" => IsEnraged ? 58f : 44f,
            "Wanderer Magican" => IsEnraged ? 54f : 40f,
            _ => IsEnraged ? 44f : 32f
        };

        var moved = false;
        if (absDistance < desiredDistance - 24f && absDistance <= WizardRangedDistance)
        {
            // Step back slightly to keep casting space
            var nextX = Math.Clamp(Position.X - _facing * moveSpeed * elapsedSeconds, _arenaLeft, _arenaRight);
            moved = Math.Abs(nextX - Position.X) > 0.05f;
            Position = new Vector2(nextX, Position.Y);

            if ((VariantId == "Lightning Mage" || VariantId == "Wanderer Magican") &&
                _jumpCooldown <= 0f && Position.Y >= _groundY - 0.5f && absDistance < 54f)
            {
                _verticalVelocity = -155f;
                _jumpCooldown = 2.6f;
            }
        }
        else if (absDistance > desiredDistance + 24f && absDistance <= WizardRangedDistance + 60f)
        {
            var nextX = Math.Clamp(Position.X + _facing * moveSpeed * elapsedSeconds, _arenaLeft, _arenaRight);
            moved = Math.Abs(nextX - Position.X) > 0.05f;
            Position = new Vector2(nextX, Position.Y);
        }

        if (Position.Y < _groundY - 1f && Character.HasAnimation(EnemyAnimationState.Jump))
        {
            Character.SetAnimation(EnemyAnimationState.Jump);
        }
        else if (moved)
        {
            Character.SetAnimation(IsEnraged && Character.HasAnimation(EnemyAnimationState.Run)
                ? EnemyAnimationState.Run
                : EnemyAnimationState.Walk);
        }
        else
        {
            Character.SetAnimation(EnemyAnimationState.Idle);
        }
    }

    private void StartWizardAttack(float absDistance)
    {
        _attackHitApplied = false;
        _rangedReleased = false;
        var pattern = _attackPatternIndex++;
        var cooldownFactor = _difficulty switch
        {
            GameDifficulty.Easy => 1.2f,
            GameDifficulty.Hard => 0.8f,
            _ => 1f
        };
        cooldownFactor *= WizardAttackCooldownMultiplier;
        if (IsEnraged)
            cooldownFactor *= 0.8f;

        if (VariantId == "Fire Wizard")
        {
            if (absDistance <= WizardMeleeDistance)
            {
                _rangedAttack = false;
                _activeAttackAnimation = pattern % 2 == 0 ? EnemyAnimationState.Attack1 : EnemyAnimationState.Attack2;
                _attackRemaining = 0.65f;
                _attackCooldown = 1.25f * cooldownFactor;
            }
            else if (absDistance <= WizardFlameJetDistance && pattern % 3 == 1)
            {
                // Sweeping flame jet melee burst
                _rangedAttack = false;
                _activeAttackAnimation = EnemyAnimationState.Special2;
                _attackRemaining = 0.95f;
                _attackCooldown = 1.55f * cooldownFactor;
            }
            else
            {
                // Fireball cast
                _rangedAttack = true;
                _activeAttackAnimation = EnemyAnimationState.Special1;
                _attackRemaining = 0.85f;
                _attackCooldown = 1.65f * cooldownFactor;
            }
        }
        else if (VariantId == "Lightning Mage")
        {
            if (absDistance <= WizardMeleeDistance - 6f)
            {
                _rangedAttack = false;
                _activeAttackAnimation = pattern % 2 == 0 ? EnemyAnimationState.Attack1 : EnemyAnimationState.Attack2;
                _attackRemaining = 0.58f;
                _attackCooldown = 1.1f * cooldownFactor;
            }
            else if (pattern % 3 == 2)
            {
                // Light_charge surge volley
                _rangedAttack = true;
                _activeAttackAnimation = EnemyAnimationState.Special2;
                _attackRemaining = 1.0f;
                _attackCooldown = 1.65f * cooldownFactor;
            }
            else
            {
                // Fast Light_ball bolt
                _rangedAttack = true;
                _activeAttackAnimation = EnemyAnimationState.Special1;
                _attackRemaining = 0.72f;
                _attackCooldown = 1.35f * cooldownFactor;
            }
        }
        else
        {
            // Wanderer Magican: blends melee staff, rapid Magic_arrow, and heavy Magic_sphere volleys
            if (absDistance <= WizardMeleeDistance)
            {
                _rangedAttack = false;
                _activeAttackAnimation = pattern % 2 == 0 ? EnemyAnimationState.Attack1 : EnemyAnimationState.Attack2;
                _attackRemaining = 0.62f;
                _attackCooldown = 1.15f * cooldownFactor;
            }
            else if (pattern % 2 == 0)
            {
                _rangedAttack = true;
                _activeAttackAnimation = EnemyAnimationState.Special1;
                _attackRemaining = 0.70f;
                _attackCooldown = 1.30f * cooldownFactor;
            }
            else
            {
                _rangedAttack = true;
                _activeAttackAnimation = EnemyAnimationState.Special2;
                _attackRemaining = 1.05f;
                _attackCooldown = 1.60f * cooldownFactor;
            }
        }

        Character.SetAnimation(_activeAttackAnimation);
    }

    public bool TryLaunchProjectiles(Vector2 targetPosition, List<EnemyProjectile> projectiles, out AudioCue castCue)
    {
        castCue = AudioCue.Magic;
        if (!IsAttacking || !_rangedAttack || _rangedReleased || _attackRemaining > 0.32f)
            return false;
        if (Character.RangedProjectileTexture is null || Character.RangedProjectileFrameCount <= 0)
            return false;

        _rangedReleased = true;
        var origin = Position + new Vector2(_facing * (BossHalfWidth + 6f), -BossHeight * 0.62f);
        var baseDirection = targetPosition - origin;
        baseDirection = baseDirection.LengthSquared() > 0.001f
            ? Vector2.Normalize(baseDirection)
            : new Vector2(_facing, 0f);

        if (VariantId == "Fire Wizard")
        {
            castCue = AudioCue.FlameImpact;
            var damage = CombatBalance.EnemyAttackDamage + 4;
            EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                origin, baseDirection, 175f, 8f, 8f, 0.75f, damage, AudioCue.FlameImpact, CombatEffectKind.Impact);
            if (IsEnraged)
            {
                EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                    origin, RotateVector(baseDirection, -0.18f), 165f, 8f, 8f, 0.70f, damage, AudioCue.FlameImpact, CombatEffectKind.Impact);
                EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                    origin, RotateVector(baseDirection, 0.18f), 165f, 8f, 8f, 0.70f, damage, AudioCue.FlameImpact, CombatEffectKind.Impact);
            }
            return true;
        }

        if (VariantId == "Lightning Mage")
        {
            castCue = AudioCue.Magic;
            var damage = CombatBalance.EnemyAttackDamage + 5;
            if (_activeAttackAnimation == EnemyAnimationState.Special2)
            {
                // Light_charge: high-speed dual/triple lightning burst
                EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                    origin, RotateVector(baseDirection, -0.14f), 240f, 8f, 7f, 0.75f, damage, AudioCue.Magic, CombatEffectKind.Magic);
                EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                    origin, RotateVector(baseDirection, 0.14f), 240f, 8f, 7f, 0.75f, damage, AudioCue.Magic, CombatEffectKind.Magic);
                if (IsEnraged)
                {
                    EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                        origin, baseDirection, 260f, 8f, 7f, 0.80f, damage, AudioCue.Magic, CombatEffectKind.Magic);
                }
            }
            else
            {
                // Light_ball: fast direct lightning orb
                EmitProjectile(projectiles, Character.RangedProjectileTexture, Character.RangedProjectileFrameCount,
                    origin, baseDirection, 235f, 8f, 8f, 0.75f, damage, AudioCue.Magic, CombatEffectKind.Magic);
            }
            return true;
        }

        // Wanderer Magican
        castCue = AudioCue.Magic;
        var arcaneDamage = CombatBalance.EnemyAttackDamage + 6;
        if (_activeAttackAnimation == EnemyAnimationState.Special2)
        {
            // Magic_sphere: heavy arcane orb fan using Charge_2
            var tex = Character.SecondaryProjectileTexture;
            var frames = Character.SecondaryProjectileFrameCount;
            EmitProjectile(projectiles, tex, frames, origin, baseDirection, 165f, 9f, 9f, 0.55f, arcaneDamage, AudioCue.Explosion, CombatEffectKind.Magic);
            EmitProjectile(projectiles, tex, frames, origin, RotateVector(baseDirection, -0.22f), 155f, 8f, 8f, 0.50f, arcaneDamage, AudioCue.Magic, CombatEffectKind.Magic);
            EmitProjectile(projectiles, tex, frames, origin, RotateVector(baseDirection, 0.22f), 155f, 8f, 8f, 0.50f, arcaneDamage, AudioCue.Magic, CombatEffectKind.Magic);
        }
        else
        {
            // Magic_arrow: rapid arcane bolts using Charge_1
            var tex = Character.RangedProjectileTexture;
            var frames = Character.RangedProjectileFrameCount;
            EmitProjectile(projectiles, tex, frames, origin, baseDirection, 245f, 8f, 6f, 0.52f, arcaneDamage, AudioCue.Magic, CombatEffectKind.Magic);
            if (IsEnraged)
            {
                EmitProjectile(projectiles, tex, frames, origin + new Vector2(0f, -10f), RotateVector(baseDirection, -0.10f), 235f, 7f, 6f, 0.48f, arcaneDamage, AudioCue.Magic, CombatEffectKind.Magic);
            }
        }
        return true;
    }

    public bool TryConsumeAttackHit(Rectangle playerHitbox)
    {
        if (!IsAttacking || _rangedAttack || _attackHitApplied || !AttackHitbox.Intersects(playerHitbox))
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
        if (Kind != BossKind.Wizard &&
            IsAlive &&
            !_recoveryApplied &&
            Health <= CombatBalance.BossRecoveryThreshold)
        {
            Health = CombatBalance.BossRecoveryHealth;
            _recoveryApplied = true;
        }
        if (IsAlive && IsStunned)
        {
            _hitsWhileStunned++;
            var hitsToBreakStun = Kind == BossKind.Wizard ? WizardHitsToBreakStun : 1;
            if (_hitsWhileStunned >= hitsToBreakStun)
            {
                _isStunned = false;
                _hitsSinceLastStun = 0;
                _hitsWhileStunned = 0;
            }
        }
        else if (IsAlive && ++_hitsSinceLastStun >= HitsToStun)
        {
            _hitsSinceLastStun = 0;
            _hitsWhileStunned = 0;
            _isStunned = true;
            _attackRemaining = 0f;
            _rangedAttack = false;
            _rangedReleased = false;
        }
        if (!IsAlive)
        {
            _isStunned = false;
            _hitsSinceLastStun = 0;
            _hitsWhileStunned = 0;
            _attackRemaining = 0f;
            _rangedAttack = false;
        }
        Character.SetAnimation(IsAlive ? EnemyAnimationState.Hurt : EnemyAnimationState.Dead);
        return true;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var effects = _facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Character.Draw(spriteBatch, Position, VisualScale, effects);
    }

    private void ApplyGravity(float elapsedSeconds)
    {
        if (Position.Y < _groundY || _verticalVelocity < 0f)
        {
            _verticalVelocity += Gravity * elapsedSeconds;
            Position = new Vector2(Position.X, Position.Y + _verticalVelocity * elapsedSeconds);
            if (Position.Y >= _groundY)
            {
                Position = new Vector2(Position.X, _groundY);
                _verticalVelocity = 0f;
            }
        }
    }

    private static void EmitProjectile(
        List<EnemyProjectile> projectiles,
        Texture2D texture,
        int frameCount,
        Vector2 origin,
        Vector2 direction,
        float speed,
        float halfWidth,
        float halfHeight,
        float scale,
        int damage,
        AudioCue impactCue,
        CombatEffectKind impactEffect)
    {
        projectiles.Add(new EnemyProjectile(
            texture,
            frameCount,
            origin,
            direction,
            speed,
            halfWidth,
            halfHeight,
            scale,
            damage,
            impactCue,
            impactEffect));
    }

    private static Vector2 RotateVector(Vector2 vector, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }

    private static float GetBossScale(string variantId) => variantId switch
    {
        "Fire Wizard" => FireWizardScale,
        "Lightning Mage" => LightningMageScale,
        "Wanderer Magican" => WandererMagicanScale,
        _ => MinotaurScale
    };

    private static bool IsVariantOf(BossKind kind, string variant) => kind switch
    {
        BossKind.Minotaur => variant is "Minotaur_1" or "Minotaur_2" or "Minotaur_3",
        BossKind.Wizard => variant is "Fire Wizard" or "Lightning Mage" or "Wanderer Magican",
        _ => false
    };
}
