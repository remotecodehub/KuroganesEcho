using System;

namespace KuroganesEcho.Core;

/// <summary>
/// Pure, platform-independent audio mixer state used by <see cref="AudioService"/>
/// for default volumes, clamping, muting, and applying persisted <see cref="GameSettings"/>.
/// </summary>
internal sealed class AudioMixerState
{
    public const float DefaultBgmVolume = GameSettings.DefaultBgmVolume;
    public const float DefaultSfxVolume = GameSettings.DefaultSfxVolume;

    public float BgmVolume { get; private set; } = DefaultBgmVolume;
    public float SfxVolume { get; private set; } = DefaultSfxVolume;
    public bool IsMuted { get; private set; }

    public float EffectiveBgmVolume => IsMuted ? 0f : BgmVolume;

    public float GetEffectiveSfxVolume(float cueVolume = 1f) =>
        IsMuted ? 0f : Math.Clamp(cueVolume, 0f, 1f) * SfxVolume;

    public void SetBgmVolume(float volume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume))
            return;

        BgmVolume = Math.Clamp(volume, 0f, 1f);
    }

    public void SetSfxVolume(float volume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume))
            return;

        SfxVolume = Math.Clamp(volume, 0f, 1f);
    }

    public void SetMuted(bool muted) => IsMuted = muted;

    public void ToggleMute() => IsMuted = !IsMuted;

    public void ApplySettings(GameSettings settings)
    {
        var effective = settings ?? GameSettings.CreateDefault();
        SetBgmVolume(effective.BgmVolume);
        SetSfxVolume(effective.SfxVolume);
        SetMuted(effective.IsMuted);
    }
}
