using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace KuroganesEcho.Core;

/// <summary>Tipo de piso sob os pés do jogador.</summary>
internal enum FootstepSurface
{
    Snow,
    Wood,
    Roof
}

/// <summary>
/// Serviço central de áudio do jogo. Mantém os efeitos carregados, reproduz SFX
/// pontuais e controla uma instância de BGM em loop quando houver um asset de música.
/// </summary>
internal sealed class AudioService : IDisposable
{
    private const int FootstepVariantsPerSurface = 3;
    private const float WalkVolume = 0.40f;
    private const float RunVolume = 0.60f;

    private readonly ContentManager _content;
    private readonly SoundEffect[][] _footstepSounds;
    private readonly Dictionary<string, SoundEffect> _soundEffects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();
    private int _lastFootstepVariant = -1;
    private SoundEffectInstance _bgmInstance;

    public float BgmVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;
    public bool IsMuted { get; private set; }

    private AudioService(ContentManager content, SoundEffect[][] footstepSounds)
    {
        _content = content;
        _footstepSounds = footstepSounds;
    }

    public static AudioService Load(ContentManager content)
    {
        var surfaces = Enum.GetValues<FootstepSurface>();
        var sounds = new SoundEffect[surfaces.Length][];
        var service = new AudioService(content, sounds);

        foreach (var surface in surfaces)
        {
            var set = new SoundEffect[FootstepVariantsPerSurface];
            for (var index = 0; index < set.Length; index++)
                set[index] = service.LoadSoundEffect($"Audio/Steps/{surface}_{index + 1:00}");
            sounds[(int)surface] = set;
        }

        return service;
    }

    /// <summary>Reproduz um SFX carregado pelo caminho lógico do Content.</summary>
    public void PlaySfx(string assetPath, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        var effectiveVolume = IsMuted ? 0f : Math.Clamp(volume, 0f, 1f) * SfxVolume;
        if (effectiveVolume > 0f)
            LoadSoundEffect(assetPath).Play(effectiveVolume, pitch, pan);
    }

    /// <summary>Reproduz uma variação de passo sem repetir a última variação global.</summary>
    public void PlayFootstep(FootstepSurface surface, bool running)
    {
        var set = _footstepSounds[(int)surface];
        int variant;
        do
        {
            variant = _random.Next(set.Length);
        } while (set.Length > 1 && variant == _lastFootstepVariant);

        _lastFootstepVariant = variant;
        var pitch = (float)(_random.NextDouble() * 0.2 - 0.1);
        var volume = (running ? RunVolume : WalkVolume) * SfxVolume;
        if (!IsMuted && volume > 0f)
            set[variant].Play(volume, pitch, 0f);
    }

    public void SetBgmVolume(float volume)
    {
        BgmVolume = Math.Clamp(volume, 0f, 1f);
        ApplyBgmVolume();
    }

    public void SetSfxVolume(float volume) => SfxVolume = Math.Clamp(volume, 0f, 1f);

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        ApplyBgmVolume();
    }

    public void ToggleMute() => SetMuted(!IsMuted);

    /// <summary>Inicia ou troca a música de fundo em loop usando o volume configurado.</summary>
    public void PlayBgm(string assetPath)
    {
        StopBgm();
        _bgmInstance = LoadSoundEffect(assetPath).CreateInstance();
        _bgmInstance.IsLooped = true;
        ApplyBgmVolume();
        _bgmInstance.Play();
    }

    public void StopBgm()
    {
        if (_bgmInstance is null)
            return;

        _bgmInstance.Stop();
        _bgmInstance.Dispose();
        _bgmInstance = null;
    }

    public void Dispose() => StopBgm();

    private SoundEffect LoadSoundEffect(string assetPath)
    {
        if (_soundEffects.TryGetValue(assetPath, out var soundEffect))
            return soundEffect;

        soundEffect = _content.Load<SoundEffect>(assetPath);
        _soundEffects.Add(assetPath, soundEffect);
        return soundEffect;
    }

    private void ApplyBgmVolume()
    {
        if (_bgmInstance is not null)
            _bgmInstance.Volume = IsMuted ? 0f : BgmVolume;
    }
}
