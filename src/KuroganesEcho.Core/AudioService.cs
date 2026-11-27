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

internal enum AudioCue
{
    UiClick,
    Collectable,
    Chain,
    Bell,
    SealRuby,
    Sword,
    SlashImpact,
    Whoosh,
    Magic,
    FlameImpact,
    Explosion
}

/// <summary>
/// Serviço central de áudio do jogo. Mantém os efeitos carregados, reproduz SFX
/// pontuais e controla uma instância de BGM em loop quando houver um asset de música.
/// </summary>
internal sealed class AudioService : IDisposable
{
    private sealed class ActiveSfx
    {
        public ActiveSfx(SoundEffectInstance instance, float remainingSeconds)
        {
            Instance = instance;
            RemainingSeconds = remainingSeconds;
        }

        public SoundEffectInstance Instance { get; }
        public float RemainingSeconds { get; set; }
    }

    private const int FootstepVariantsPerSurface = 3;
    private const float WalkVolume = 0.40f;
    private const float RunVolume = 0.60f;
    private const float FootstepMaxDuration = 0.35f;

    private readonly ContentManager _content;
    private readonly SoundEffect[][] _footstepSounds;
    private readonly Dictionary<string, SoundEffect> _soundEffects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ActiveSfx> _activeSfx = new();
    private readonly Random _random = new();
    private readonly AudioMixerState _mixer = new();
    private int _lastFootstepVariant = -1;
    private SoundEffectInstance _bgmInstance;
    private string _currentBgmAsset;

    public const float DefaultBgmVolume = AudioMixerState.DefaultBgmVolume;
    public const float DefaultSfxVolume = AudioMixerState.DefaultSfxVolume;

    public float BgmVolume => _mixer.BgmVolume;
    public float SfxVolume => _mixer.SfxVolume;
    public bool IsMuted => _mixer.IsMuted;

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

    public void ApplySettings(GameSettings settings)
    {
        _mixer.ApplySettings(settings);
        ApplyBgmVolume();
    }

    /// <summary>Reproduz um SFX carregado pelo caminho lógico do Content.</summary>
    public void PlaySfx(string assetPath, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        PlaySfxInstance(assetPath, volume, pitch, pan, null);
    }

    public void PlayCue(AudioCue cue, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        var assetPath = cue switch
        {
            AudioCue.UiClick => "Audio/SFX/SFX_Interface_Click",
            AudioCue.Collectable => "Audio/SFX/SFX_Collectable_UI_Sound",
            AudioCue.Chain => "Audio/SFX/SFX-Chain",
            AudioCue.Bell => "Audio/SFX/SFX--Bell",
            AudioCue.SealRuby => "Audio/SFX/SFX--rubi",
            AudioCue.Sword => "Audio/SFX/SFX_Sword_Sound",
            AudioCue.SlashImpact => "Audio/SFX/SFX_Sword_Slash_With_Metallic_Impact",
            AudioCue.Whoosh => "Audio/SFX/SFX_Whoosh_Slash_Pitches_Vol2",
            AudioCue.Magic => "Audio/SFX/SFX_Magic_SFX_for_Games",
            AudioCue.FlameImpact => "Audio/SFX/SFX_Flame_Spell_Impact",
            AudioCue.Explosion => "Audio/SFX/SFX_Game_Explosion",
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown audio cue")
        };

        PlaySfxInstance(assetPath, volume, pitch, pan, CueMaxDuration(cue));
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
            PlaySfxInstance(set[variant], volume, pitch, 0f, FootstepMaxDuration);
    }

    /// <summary>
    /// Atualiza e encerra instâncias pontuais. Alguns assets fornecidos são
    /// bancos de efeitos ou têm caudas muito maiores que o efeito visual.
    /// </summary>
    public void Update(float elapsedSeconds)
    {
        for (var index = _activeSfx.Count - 1; index >= 0; index--)
        {
            var active = _activeSfx[index];
            active.RemainingSeconds -= Math.Max(0f, elapsedSeconds);
            if (active.RemainingSeconds <= 0f || active.Instance.State != SoundState.Playing)
            {
                active.Instance.Stop();
                active.Instance.Dispose();
                _activeSfx.RemoveAt(index);
            }
        }
    }

    public void SetBgmVolume(float volume)
    {
        _mixer.SetBgmVolume(volume);
        ApplyBgmVolume();
    }

    public void SetSfxVolume(float volume) => _mixer.SetSfxVolume(volume);

    public void SetMuted(bool muted)
    {
        _mixer.SetMuted(muted);
        ApplyBgmVolume();
    }

    public void ToggleMute()
    {
        _mixer.ToggleMute();
        ApplyBgmVolume();
    }

    /// <summary>Inicia ou troca a música de fundo em loop usando o volume configurado.</summary>
    public void PlayBgm(string assetPath)
    {
        if (string.Equals(_currentBgmAsset, assetPath, StringComparison.OrdinalIgnoreCase) &&
            _bgmInstance is not null &&
            _bgmInstance.State == SoundState.Playing)
            return;

        StopAllSfx();
        StopBgm();
        _currentBgmAsset = assetPath;
        _bgmInstance = LoadSoundEffect(assetPath).CreateInstance();
        _bgmInstance.IsLooped = true;
        ApplyBgmVolume();
        _bgmInstance.Play();
    }

    public void PlayIntroMusic() => PlayBgm("Audio/BGM/BGM_Japan_Epic_Music");

    public void PlayMenuMusic() => PlayBgm("Audio/BGM/BGM_Samurai_and_horse");

    public void PlayActMusic(string levelId)
    {
        var assetPath = levelId switch
        {
            "Act_01_C" or "Act_02_C" => "Audio/BGM/BGM--Bossfight2",
            "Act_03_C" => "Audio/BGM/BGM--FinalBossFight",
            "Act_01_B" => "Audio/BGM/BGM_Japanese_Dark_Temple_Samurai",
            "Act_03_B" => "Audio/BGM/BGM_Kakashi_Samurai_or_Scarecrow",
            _ => "Audio/BGM/BGM_Along_The_Mountain_Path"
        };
        PlayBgm(assetPath);
    }

    public void PlayFinalBossMusic() => PlayBgm("Audio/BGM/BGM--FinalBossFight");

    public void PlayGameOverMusic() => PlayBgm("Audio/BGM/BGM_Kakashi_Samurai_or_Scarecrow");

    public void PlayVictoryMusic() => PlayBgm("Audio/BGM/BGM_Japan_Epic_Music");

    public void PlayCreditsMusic() => PlayBgm("Audio/BGM/BGM_Samurai_and_horse");

    public void StopBgm()
    {
        if (_bgmInstance is null)
            return;

        _bgmInstance.Stop();
        _bgmInstance.Dispose();
        _bgmInstance = null;
        _currentBgmAsset = null;
    }

    public void Dispose()
    {
        StopAllSfx();
        StopBgm();
    }

    public void StopAllSfx()
    {
        foreach (var active in _activeSfx)
        {
            active.Instance.Stop();
            active.Instance.Dispose();
        }

        _activeSfx.Clear();
    }

    private void PlaySfxInstance(
        string assetPath,
        float volume,
        float pitch,
        float pan,
        float? maxDuration)
    {
        PlaySfxInstance(LoadSoundEffect(assetPath), volume, pitch, pan, maxDuration);
    }

    private void PlaySfxInstance(
        SoundEffect soundEffect,
        float volume,
        float pitch,
        float pan,
        float? maxDuration)
    {
        var effectiveVolume = _mixer.GetEffectiveSfxVolume(volume);
        if (effectiveVolume <= 0f)
            return;

        var instance = soundEffect.CreateInstance();
        instance.Volume = effectiveVolume;
        instance.Pitch = Math.Clamp(pitch, -1f, 1f);
        instance.Pan = Math.Clamp(pan, -1f, 1f);
        instance.Play();
        _activeSfx.Add(new ActiveSfx(instance, maxDuration ?? 30f));
    }

    private static float CueMaxDuration(AudioCue cue) => cue switch
    {
        AudioCue.UiClick => 0.70f,
        AudioCue.Collectable => 0.80f,
        AudioCue.Chain => 1.15f,
        AudioCue.Bell => 3.65f,
        AudioCue.SealRuby => 10f,
        AudioCue.Sword => 0.50f,
        AudioCue.SlashImpact => 0.45f,
        // The source is a 16-second whoosh bank. The first whoosh ends near
        // 0.30 s; stopping the instance prevents the later samples sounding
        // like an echo after the attack effect has disappeared.
        AudioCue.Whoosh => 0.36f,
        AudioCue.Magic => 0.70f,
        AudioCue.FlameImpact => 0.65f,
        AudioCue.Explosion => 0.85f,
        _ => 0.5f
    };

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
            _bgmInstance.Volume = _mixer.EffectiveBgmVolume;
    }
}
