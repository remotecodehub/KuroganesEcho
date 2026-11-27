using System;
using System.Collections.Generic;
using System.Globalization;
using KuroganesEcho.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace KuroganesEcho.Core;

/// <summary>
/// Playable shell for the first act, including the A/B map progression,
/// difficulty scaling, lives, stamina, combat effects and localized UI.
/// </summary>
public class KuroganesEchoGame : Game
{
    private const int VirtualWidth = 320;
    private const int VirtualHeight = 180;
    private const float EnemySpawnActivationDistance = VirtualWidth / 2f + 64f;
    private const float PlayerSpeed = 72f;
    private const float RunSpeed = 132f;
    private const float PlayerScale = 0.5f;
    private const float Gravity = 480f;
    private const float JumpVelocity = -180f;
    private const float DoubleJumpVelocity = -180f;
    private const float RunningDoubleJumpVelocity = -304f;
    private const float PlayerHalfWidth = 10f;
    private const int MaximumJumps = 2;
    private const float PlayerHeight = 32f;
    private const float IntroScrollSpeed = 8f;
    private const float IntroLineHeight = 13f;
    private const float CreditsScrollSpeed = 14f;
    private const float CreditsLineHeight = 12f;
    private const float BossBannerDuration = 2.6f;
    private const float StaminaMaximum = 5f;
    private const float StaminaRecoveryPerSecond = 0.85f;
    private const float RunStaminaCost = 1f;
    private const float LevelBriefingDuration = 5f;
    private const int StartingLives = 3;
    private const int MaximumLives = 5;
    private static readonly string[] IntroLineKeys =
    {
        "IntroLine01", "IntroLine02", "IntroLine03", "IntroLine04", "IntroLine05",
        "IntroLine06", "IntroLine07", "IntroLine08", "IntroLine09", "IntroLine10",
        "IntroLine11", "IntroLine12", "IntroLine13"
    };
    private static readonly string[] CreditsLineKeys =
    {
        "CreditsHeadingComplete",
        "CreditsSpacer",
        "CreditsLineSealRestored",
        "CreditsLineFlameLives",
        "CreditsSpacer",
        "CreditsSectionDirection",
        "CreditsDirectionName",
        "CreditsSpacer",
        "CreditsSectionEngineering",
        "CreditsEngineeringName",
        "CreditsSpacer",
        "CreditsSectionArt",
        "CreditsArtName",
        "CreditsSpacer",
        "CreditsSectionAudio",
        "CreditsAudioName",
        "CreditsSpacer",
        "CreditsSectionTech",
        "CreditsTechName",
        "CreditsSpacer",
        "CreditsThankYou"
    };

    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _menuKeys = { "Play", "Continue", "Options", "Quit" };
    private readonly string[] _difficultyKeys = { "Easy", "Normal", "Hard" };
    private readonly string[] _controlKeys =
    {
        "MoveLeft", "MoveRight", "Attack1", "Attack2", "Jump", "Run",
        "SpecialPrimary", "SpecialSecondary"
    };
    private readonly Keys[] _keyboardBindings = { Keys.A, Keys.D, Keys.Z, Keys.X, Keys.C, Keys.R, Keys.F, Keys.V };
    private readonly Buttons[] _gamepadBindings =
    {
        Buttons.DPadLeft, Buttons.DPadRight, Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder
    };
    private static readonly Buttons[] ConfigurableGamepadButtons =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.Back
    };

    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private SpriteFont _fontSemibold = null!;
    private SpriteFont _fontWide = null!;
    private SpriteFont _fontSemiboldWide = null!;
    private Texture2D _whiteTexture = null!;
    private Texture2D _mainBackground = null!;
    private Texture2D _gameplayBackground = null!;
    private PlayerCharacter _player = null!;
    private GameLevel _level = null!;
    private AudioService _audio = null!;
    private readonly List<EnemyInstance> _enemies = new();
    private readonly List<LevelObjectData> _pendingEnemySpawns = new();
    private readonly List<PlayerProjectile> _projectiles = new();
    private readonly List<EnemyProjectile> _enemyProjectiles = new();
    private BossInstance _boss;
    private LevelObjectData _act03BossSpawn;
    private readonly Act03EncounterStateMachine _act03Encounter = new();
    private string _bossBannerText = string.Empty;
    private float _bossBannerRemaining;
    private readonly CombatEffectSystem _effects = new();
    private SaveProgressState _saveProgress = new();
    private GameSettings _settings = GameSettings.CreateDefault();
    private LanguageSelector _languageSelector = new();
    private readonly HorizontalSwipeDetector _languageSwipeDetector = new();
    private readonly SaveSlotInputController _pauseSlotInput = new();
    private readonly SaveSlotInputController _loadSlotInput = new();
    private string _cachedIntroCulture = string.Empty;
    private List<IntroParagraphLine> _cachedIntroLines = new();
    private BoxingViewportAdapter _viewportAdapter = null!;
    private OrthographicCamera _camera = null!;
    private readonly string[] _characterIds = { "Kunoichi", "Ninja_Monk", "Ninja_Peasant" };
    private Texture2D _spineTexture = null!;
    private Texture2D _kunaiTexture = null!;
    private Texture2D _dartTexture = null!;

    private GameScreen _screen = GameScreen.Intro;
    private float _introScroll;
    private float _creditsScroll;
    private int _menuSelection;
    private int _optionsSelection;
    private int _characterSelection;
    private int _difficultySelection = 1;
    private GameDifficulty _selectedDifficulty = GameDifficulty.Normal;
    private int _controlSelection;
    private int _pauseSelection;
    private string _pauseStatusMessage = string.Empty;
    private int _slotSelection;
    private string _slotStatusMessage = string.Empty;
    private GameScreen _slotSelectionReturnScreen = GameScreen.MainMenu;
    private GameScreen _optionsReturnScreen = GameScreen.MainMenu;
    private BindingDevice _bindingDevice = BindingDevice.Keyboard;
    private bool _waitingForBinding;
    private bool _bindingReady;
    private HashSet<Keys> _ignoredBindingKeys = new();
    private HashSet<Buttons> _ignoredBindingButtons = new();
    private Vector2 _playerPosition = new(64f, 352f);
    private Vector2 _moveInput;
    private Vector2 _analogVisualInput;
    private float _verticalVelocity;
    private bool _isGrounded;
    private float _runRemaining;
    private float _stamina = StaminaMaximum;
    private float _power;
    private float _powerRecoveryElapsed;
    private float _playerInvulnerability;
    private float _playerKnockbackVelocity;
    private int _jumpsUsed;
    private int _playerHealth = CombatBalance.PlayerMaxHealth;
    private int _livesRemaining = 3;
    private int _checkpointId;
    private Vector2 _checkpointPosition;
    private int _playerFacing = 1;
    private bool _playerAttackHitApplied;
    private readonly HashSet<string> _collectedCollectibles = new(StringComparer.OrdinalIgnoreCase);
    private float _levelBriefingRemaining;
    private float _atmosphereTime;
    private bool _finalBossDefeated;
    private bool _finalBossRewardCollected;
    private KeyboardState _previousKeyboard;
    private GamePadState _previousGamePad;
    private bool _gamepadConnected;
    private MouseState _previousMouse;
    private bool _fullScreen;
    private string _currentLevelId = "Act_01_A";
#if DEBUG
    private readonly DebugCheatCodeSystem _debugCheatCodes = new();
    private const float MobileCheatLongPressDuration = 0.85f;
    private int _mobileCheatLongPressTouchId = -1;
    private float _mobileCheatLongPressElapsed;
    private bool _mobileCheatLongPressTriggered;
    private string _visualValidationDir;
    private int _visualValidationStep;
    private int _visualValidationFrameWait;
    private bool _visualValidationCapturedStep;
#endif

    public static bool IsMobile => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
    public static bool IsDesktop => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    public KuroganesEchoGame()
    {
        _settings = GameSettingsStore.Load(defaultFullScreen: IsDesktop);
        _languageSelector = new LanguageSelector(initialCultureCode: _settings.Language);
        LocalizationManager.SetCulture(_languageSelector.SelectedCode);

        var startFullScreen = IsDesktop && _settings.FullScreen;
#if DEBUG
        _visualValidationDir = Environment.GetEnvironmentVariable("KUROGANE_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(_visualValidationDir))
            startFullScreen = false;
#endif
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = startFullScreen
        };
        _fullScreen = startFullScreen;

        Services.AddService(typeof(GraphicsDeviceManager), _graphics);
        Content.RootDirectory = "Content";
        _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
    }

    protected override void Initialize()
    {
        LocalizationManager.SetCulture(_languageSelector.SelectedCode);
        if (IsDesktop && _graphics.IsFullScreen != _fullScreen)
        {
            _graphics.IsFullScreen = _fullScreen;
            _graphics.ApplyChanges();
        }

        _saveProgress = SaveProgressStore.Load();
        _livesRemaining = Math.Clamp(_saveProgress.LivesRemaining, 1, MaximumLives);
        _currentLevelId = "Act_01_A";
        if (Enum.IsDefined(typeof(GameDifficulty), _saveProgress.Difficulty))
        {
            _selectedDifficulty = _saveProgress.Difficulty;
            _difficultySelection = (int)_selectedDifficulty;
        }
        var savedCharacterIndex = Array.IndexOf(_characterIds, _saveProgress.CharacterId);
        if (savedCharacterIndex >= 0)
            _characterSelection = savedCharacterIndex;
        _viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, VirtualWidth, VirtualHeight);
        TouchPanel.EnableMouseTouchPoint = true;
        TouchPanel.DisplayWidth = GraphicsDevice.Viewport.Width;
        TouchPanel.DisplayHeight = GraphicsDevice.Viewport.Height;
        _camera = new OrthographicCamera(_viewportAdapter)
        {
            Zoom = 1f
        };

        base.Initialize();
    }

    protected override void LoadContent()
    {
        FontInstaller.TryInstallBundledFonts();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/ArrayRegular");
        _fontSemibold = Content.Load<SpriteFont>("Fonts/ArraySemibold");
        _fontWide = Content.Load<SpriteFont>("Fonts/ArrayWide");
        _fontSemiboldWide = Content.Load<SpriteFont>("Fonts/ArraySemiboldWide");
        _mainBackground = Content.Load<Texture2D>("MAIN_BG");
        _gameplayBackground = Content.Load<Texture2D>("GAMEPLAY_BG");
        _level = GameLevel.Load(GraphicsDevice, LevelDefinition.Act01A);
        _level.ConfigureDifficulty(_selectedDifficulty);
        _audio = AudioService.Load(Content);
        _audio.ApplySettings(_settings);
        _audio.PlayIntroMusic();
        _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
        _spineTexture = Content.Load<Texture2D>("Tiled/Chars/GeneratedFrames/Kunoichi/Spine");
        _kunaiTexture = Content.Load<Texture2D>("Tiled/Chars/GeneratedFrames/Ninja_Monk/Kunai");
        _dartTexture = Content.Load<Texture2D>("Tiled/Chars/GeneratedFrames/Ninja_Peasant/Dart");
        LoadEnemies();

        _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        _whiteTexture.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        _audio?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        var keyboard = Keyboard.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.IndependentAxes);
        _gamepadConnected = gamePad.IsConnected;
        if (IsDesktop && IsNewKeyPress(keyboard, Keys.F11))
            ToggleFullscreen();
        var backPressed = IsNewKeyPress(keyboard, Keys.Escape) || IsNewButtonPress(gamePad, Buttons.Back);
        var touchState = TouchPanel.GetState();

#if DEBUG
        var mobileDebugLongPressHandled = UpdateMobileDebugLongPress(gameTime, touchState);
        if (_screen == GameScreen.Paused)
        {
            var debugMenuHandled = _debugCheatCodes.Update(
                gamePad,
                _previousGamePad,
                touchState,
                IsMobile,
                ToVirtualPoint,
                GetDebugCheckpointOptions());
            ApplyDebugCheatEffects();
            if (debugMenuHandled || mobileDebugLongPressHandled)
            {
                _previousKeyboard = keyboard;
                _previousGamePad = gamePad;
                _previousMouse = Mouse.GetState();
                base.Update(gameTime);
                return;
            }
        }
        else if (_screen == GameScreen.Act01)
        {
            ApplyDebugCheatEffects();
        }
#endif

#if DEBUG
        if (!string.IsNullOrWhiteSpace(_visualValidationDir))
        {
            UpdateVisualValidation(gameTime);
            base.Update(gameTime);
            return;
        }
#endif

        if (_screen == GameScreen.Act01 &&
            (IsNewButtonPress(gamePad, Buttons.Start) ||
             backPressed ||
             (IsMobile && IsNewTouchPress(touchState, PauseTouchButton()))))
        {
            EnterPauseMenu();
        }
        else if (backPressed && _screen != GameScreen.Paused)
        {
            if (_screen == GameScreen.MainMenu)
                Exit();
            else if (_screen == GameScreen.DifficultySelection)
                _screen = GameScreen.PlayerSelection;
            else if (_screen == GameScreen.PlayerSelection)
                _screen = GameScreen.MainMenu;
            else if (_screen == GameScreen.Controls)
                _screen = GameScreen.Options;
            else if (_screen == GameScreen.Options)
                ReturnFromOptions();
            else if (_screen == GameScreen.SaveSlotSelection)
                _screen = _slotSelectionReturnScreen;
            else if (_screen is GameScreen.GameOver or GameScreen.Victory or GameScreen.Credits)
            {
                _creditsScroll = 0f;
                _screen = GameScreen.MainMenu;
                _audio.PlayMenuMusic();
            }
            else
            {
                _screen = GameScreen.MainMenu;
                _audio.PlayMenuMusic();
            }
        }
        else if (_screen == GameScreen.MainMenu)
        {
            UpdateMainMenu(keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.Intro)
        {
            UpdateIntro(gameTime, keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.Options)
        {
            UpdateOptions(keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.PlayerSelection)
        {
            UpdatePlayerSelection(keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.DifficultySelection)
        {
            UpdateDifficultySelection(keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.Controls)
        {
            UpdateControls(keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.Paused)
        {
            UpdatePaused(gameTime, keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.SaveSlotSelection)
        {
            UpdateSaveSlotSelection(gameTime, keyboard, gamePad, touchState);
        }
        else if (_screen == GameScreen.GameOver)
        {
            if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A) || GetPressedPoint(touchState).HasValue)
                _screen = GameScreen.MainMenu;
        }
        else if (_screen == GameScreen.Victory)
        {
            if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A) || GetPressedPoint(touchState).HasValue)
            {
                _screen = GameScreen.MainMenu;
                _audio.PlayMenuMusic();
            }
        }
        else if (_screen == GameScreen.Credits)
        {
            UpdateCredits(gameTime, keyboard, gamePad, touchState);
        }
        else
        {
            UpdateAct01(gameTime, keyboard, gamePad, touchState);
        }

        _previousKeyboard = keyboard;
        _previousGamePad = gamePad;
        _previousMouse = Mouse.GetState();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_screen == GameScreen.Act01)
            DrawAct01(gameTime);
        else if (_screen == GameScreen.GameOver)
            DrawGameOver();
        else if (_screen == GameScreen.Victory)
            DrawVictory();
        else if (_screen == GameScreen.Credits)
            DrawCredits();
        else if (_screen == GameScreen.Intro)
            DrawIntro();
        else if (_screen == GameScreen.Options)
            DrawOptions();
        else if (_screen == GameScreen.PlayerSelection)
            DrawPlayerSelection();
        else if (_screen == GameScreen.DifficultySelection)
            DrawDifficultySelection();
        else if (_screen == GameScreen.Controls)
            DrawControls();
        else if (_screen == GameScreen.Paused)
        {
            DrawAct01(gameTime);
            DrawPauseOverlay();
        }
        else if (_screen == GameScreen.SaveSlotSelection)
        {
            if (_slotSelectionReturnScreen == GameScreen.Paused)
            {
                DrawAct01(gameTime);
                DrawSaveSlotSelectionOverlay(true);
            }
            else
            {
                DrawSaveSlotSelectionOverlay(false);
            }
        }
        else
            DrawMainMenu();

#if DEBUG
        if (_screen == GameScreen.Paused)
        {
            BeginUi();
            if (_debugCheatCodes.IsOpen)
                _debugCheatCodes.Draw(_spriteBatch, _whiteTexture, _font, _fontSemiboldWide);
            _debugCheatCodes.DrawInputHud(_spriteBatch, _whiteTexture, _font);
            EndUi();
        }
        if (!string.IsNullOrWhiteSpace(_visualValidationDir) && _visualValidationFrameWait == 1 && !_visualValidationCapturedStep)
        {
            _visualValidationCapturedStep = true;
            CaptureVisualValidationFrame();
        }
#endif

        base.Draw(gameTime);
    }

#if DEBUG
    private void UpdateVisualValidation(GameTime gameTime)
    {
        if (_visualValidationFrameWait > 1)
        {
            _visualValidationFrameWait--;
            if (_screen == GameScreen.Act01)
            {
                var dt = 0.016f;
                _atmosphereTime += dt;
                _effects.Update(dt);
                foreach (var enemy in _enemies)
                    enemy.Update(dt, _playerPosition, _level, CameraWorldBounds);
                if (_boss is not null)
                {
                    _boss.Update(dt, _playerPosition);
                    _boss.TryLaunchProjectiles(_playerPosition - new Vector2(0f, PlayerHeight / 2f), _enemyProjectiles, out _);
                }
                UpdateEnemyProjectiles(dt);
                UpdateCamera();
            }
            else if (_screen == GameScreen.Credits)
            {
                _creditsScroll += 0.016f * CreditsScrollSpeed;
            }
            return;
        }

        if (_visualValidationFrameWait == 1)
        {
            // Wait until Draw() has captured this step before advancing to the next step.
            if (!_visualValidationCapturedStep)
                return;
            _visualValidationFrameWait = 0;
        }

        _visualValidationCapturedStep = false;
        switch (_visualValidationStep)
        {
            case 0:
                // Verify Act 01 & Act 02 regression load cleanly
                foreach (var regressionId in new[] { "Act_01_A", "Act_01_B", "Act_01_C", "Act_02_A", "Act_02_B", "Act_02_C" })
                {
                    LoadLevel(regressionId);
                    StartAct01(regressionId);
                }
                // Verify Act 02 C -> Act 03 A transition requires Minotaur_2 + Ash Seal 2/4
                _saveProgress.SealAshesParts = 2;
                _saveProgress.Act03FinalBossesCompleted = false;
                LoadLevel("Act_03_A");
                StartAct01("Act_03_A");
                _levelBriefingRemaining = 0f;
                _playerPosition = new Vector2(540f, 320f);
                ActivatePendingEnemySpawns();
                UpdateCamera();
                _visualValidationFrameWait = 6;
                break;

            case 1:
                LoadLevel("Act_03_B");
                StartAct01("Act_03_B");
                _levelBriefingRemaining = 0f;
                _playerPosition = new Vector2(540f, 320f);
                ActivatePendingEnemySpawns();
                UpdateCamera();
                _visualValidationFrameWait = 6;
                break;

            case 2:
                // Also verify legacy save with SealAshesParts=3 and Act03Seal3RestorationApplied=false
                // STILL spawns Minotaur_3 first and NEVER spawns a wizard immediately.
                _saveProgress.SealAshesParts = 3;
                _saveProgress.Act03Seal3RestorationApplied = false;
                _saveProgress.Act03FinalBossesCompleted = false;
                LoadLevel("Act_03_C");
                StartAct01("Act_03_C");
                if (_boss is null || _boss.Kind != BossKind.Minotaur || _boss.VariantId != "Minotaur_3")
                    throw new InvalidOperationException($"Expected Minotaur_3 on Act_03_C entry, got {_boss?.VariantId ?? "null"}");
                if (!_boss.Character.HasAnimation(EnemyAnimationState.Idle) ||
                    !_boss.Character.HasAnimation(EnemyAnimationState.Walk) ||
                    !_boss.Character.HasAnimation(EnemyAnimationState.Attack1) ||
                    !_boss.Character.HasAnimation(EnemyAnimationState.Hurt) ||
                    !_boss.Character.HasAnimation(EnemyAnimationState.Dead))
                    throw new InvalidOperationException("Minotaur_3 is missing required animation states.");
                _levelBriefingRemaining = 0f;
                _playerPosition = new Vector2(540f, 320f);
                ActivatePendingEnemySpawns();
                UpdateCamera();
                _visualValidationFrameWait = 6;
                break;

            case 3:
                // Minotaur_3 encounter at boss spawn: verify Walk + Attack1 + Hurt animations
                _playerPosition = new Vector2(3600f, 320f);
                _enemies.Clear();
                _boss!.Update(0.05f, _playerPosition);
                if (_boss.Character.Animation != EnemyAnimationState.Walk)
                    throw new InvalidOperationException($"Expected Minotaur_3 Walk when player approaches, got {_boss.Character.Animation}");
                _playerPosition = new Vector2(3680f, 320f);
                _boss.Update(0.05f, _playerPosition);
                if (_boss.Character.Animation != EnemyAnimationState.Attack1)
                    throw new InvalidOperationException($"Expected Minotaur_3 Attack1 in range, got {_boss.Character.Animation}");
                UpdateCamera();
                _visualValidationFrameWait = 8;
                break;

            case 4:
                // Defeat Minotaur_3 -> verify Ash Seal 3/4 is visibly revealed at (236,19) and NO wizard spawns even after waiting 2 full seconds
                _boss?.TakeDamage(9999, _boss.Position.Y - 24f);
                for (var i = 0; i < 120; i++)
                {
                    _boss?.Update(0.05f, _playerPosition);
                    UpdateBossProgression();
                }
                // Wait 2.0 seconds while player stays at fighting spot (x=3680) without touching Seal 3/4 (x=3776)
                for (var i = 0; i < 40; i++)
                {
                    _act03Encounter.Update(0.05f);
                    UpdateBossProgression();
                    CollectLevelItems();
                }
                if (_act03Encounter.Stage != Act03BossStage.AwaitingSeal3 || _boss is not null)
                    throw new InvalidOperationException($"Expected AwaitingSeal3 with NO wizard active after Minotaur_3 defeat, got {_act03Encounter.Stage} / {_boss?.VariantId ?? "null"}");
                var seal3Visible = false;
                foreach (var c in _level.Collectibles)
                {
                    if (IsSealCollectible(c) && GetSealPartIndex(c) == 3 && IsSealCollectibleVisible(c) && IsSealCollectibleAvailable(c))
                        seal3Visible = true;
                }
                if (!seal3Visible)
                    throw new InvalidOperationException("Ash Seal 3/4 is not visibly revealed and collectible after Minotaur_3 defeat.");
                _playerPosition = new Vector2(3710f, 320f);
                UpdateCamera();
                _visualValidationFrameWait = 6;
                break;

            case 5:
                // Deplete resources -> walk player onto Ash Seal 3/4 at (236,19) -> x=3784, y=320 -> verify restoration & Fire Wizard spawn
                _livesRemaining = 1;
                _playerHealth = 15;
                _stamina = 0.5f;
                _power = 0f;
                _playerPosition = new Vector2(3784f, 320f);
                CollectLevelItems();
                if (_livesRemaining != 3 ||
                    _playerHealth != CombatBalance.GetPlayerMaxHealth(_selectedDifficulty) ||
                    Math.Abs(_stamina - StaminaMaximum) > 0.01f ||
                    Math.Abs(_power - CombatBalance.PlayerPowerMaximum) > 0.01f)
                    throw new InvalidOperationException($"Resource restoration failed: Lives={_livesRemaining} HP={_playerHealth} Stam={_stamina} Pwr={_power}");
                if (!_saveProgress.Act03Seal3RestorationApplied)
                    throw new InvalidOperationException("Expected Act03Seal3RestorationApplied=true after Seal 3/4 collection.");
                if (_boss is null || _boss.VariantId != "Fire Wizard")
                    throw new InvalidOperationException($"Expected Fire Wizard after Seal 3/4 collection, got {_boss?.VariantId ?? "null"}");
                _playerPosition = new Vector2(3660f, 320f);
                UpdateCamera();
                _visualValidationFrameWait = 12;
                break;

            case 6:
                // Defeat Fire Wizard -> spawns Lightning Mage
                _boss?.TakeDamage(9999, _boss.Position.Y - 24f);
                for (var i = 0; i < 120; i++)
                {
                    _boss?.Update(0.05f, _playerPosition);
                    UpdateBossProgression();
                }
                if (_boss is null || _boss.VariantId != "Lightning Mage")
                    throw new InvalidOperationException($"Expected Lightning Mage after Fire Wizard defeat, got {_boss?.VariantId ?? "null"}");
                _playerPosition = new Vector2(3660f, 320f);
                UpdateCamera();
                _visualValidationFrameWait = 12;
                break;

            case 7:
                // Defeat Lightning Mage -> spawns Wanderer Magican
                _boss?.TakeDamage(9999, _boss.Position.Y - 24f);
                for (var i = 0; i < 120; i++)
                {
                    _boss?.Update(0.05f, _playerPosition);
                    UpdateBossProgression();
                }
                if (_boss is null || _boss.VariantId != "Wanderer Magican")
                    throw new InvalidOperationException($"Expected Wanderer Magican after Lightning Mage defeat, got {_boss?.VariantId ?? "null"}");
                _playerPosition = new Vector2(3660f, 320f);
                UpdateCamera();
                _visualValidationFrameWait = 12;
                break;

            case 8:
                // Defeat Wanderer Magican -> wait grace -> collect Ash Seal 4/4 at (238,19) -> x=3816, y=320 -> opens Credits
                _boss?.TakeDamage(9999, _boss.Position.Y - 24f);
                for (var i = 0; i < 120; i++)
                {
                    _boss?.Update(0.05f, _playerPosition);
                    UpdateBossProgression();
                }
                _act03Encounter.Update(Act03EncounterStateMachine.SealCollectionDelaySeconds);
                _playerPosition = new Vector2(3816f, 320f);
                CollectLevelItems();
                if (_act03Encounter.Stage == Act03BossStage.Completed && _saveProgress.SealAshesParts >= 4)
                    EnterCreditsScreen();
                else
                    throw new InvalidOperationException($"Expected Completed & Seal 4/4, got {_act03Encounter.Stage} & {_saveProgress.SealAshesParts}/4");
                _creditsScroll = 95f;
                _visualValidationFrameWait = 6;
                break;

            default:
                Exit();
                break;
        }
    }

    private void CaptureVisualValidationFrame()
    {
        var names = new[]
        {
            "01_Act_03_A.png",
            "02_Act_03_B.png",
            "03_Act_03_C.png",
            "04_Minotaur_3.png",
            "04b_Ash_Seal_3_Waiting.png",
            "05_Fire_Wizard.png",
            "06_Lightning_Mage.png",
            "07_Wanderer_Magican.png",
            "08_Credits.png"
        };
        if (_visualValidationStep >= 0 && _visualValidationStep < names.Length)
        {
            System.IO.Directory.CreateDirectory(_visualValidationDir);
            var w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var h = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            GraphicsDevice.GetBackBufferData(data);
            using var tex = new Texture2D(GraphicsDevice, w, h);
            tex.SetData(data);
            var outPath = System.IO.Path.Combine(_visualValidationDir, names[_visualValidationStep]);
            using var stream = System.IO.File.Create(outPath);
            tex.SaveAsPng(stream, w, h);
            var logLine = $"Captured {names[_visualValidationStep]} | Screen={_screen} Level={_currentLevelId} Stage={_act03Encounter.Stage} Boss={_boss?.VariantId ?? "None"} Scale={_boss?.VisualScale ?? 0f:F2} Seal={_saveProgress.SealAshesParts}/4 Lives={_livesRemaining} HP={_playerHealth} Stam={_stamina:F1} Pwr={_power:F1} Enemies={_enemies.Count}";
            System.IO.File.AppendAllText(System.IO.Path.Combine(_visualValidationDir, "validation.log"), logLine + Environment.NewLine);
        }
        _visualValidationStep++;
    }
#endif

    private void UpdateMainMenu(KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewGamepadUp(gamePad))
            _menuSelection = (_menuSelection + _menuKeys.Length - 1) % _menuKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewGamepadDown(gamePad))
            _menuSelection = (_menuSelection + 1) % _menuKeys.Length;

        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            for (var index = 0; index < _menuKeys.Length; index++)
            {
                if (MenuButton(index).Contains(point.Value))
                {
                    _menuSelection = index;
                    ActivateMainMenuSelection();
                    return;
                }
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            ActivateMainMenuSelection();
    }

    private void EnterPauseMenu()
    {
        _pauseSelection = 0;
        _pauseStatusMessage = string.Empty;
        _pauseSlotInput.Reset();
        _screen = GameScreen.Paused;
    }

    private void UpdatePaused(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
#if DEBUG
        if (UpdateMobileDebugLongPressState(touches))
            return;
#endif

        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var touchInputs = ConvertTouchesToVirtual(touches);

        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewGamepadUp(gamePad))
            _pauseSelection = (_pauseSelection + PauseOptionCount - 1) % PauseOptionCount;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewGamepadDown(gamePad))
            _pauseSelection = (_pauseSelection + 1) % PauseOptionCount;

        if (IsNewKeyPress(keyboard, Keys.Escape) ||
            IsNewButtonPress(gamePad, Buttons.Back) ||
            IsNewButtonPress(gamePad, Buttons.Start))
        {
            _pauseSlotInput.Reset();
            _screen = GameScreen.Act01;
            return;
        }

        var selectedSlot = GetSelectedPauseSlot();
        var gamepadBResult = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot,
            gamepadBPressed: IsNewButtonPress(gamePad, Buttons.B),
            isSlotOccupied: SaveProgressStore.HasSlot,
            allowBackWhenNotDeleting: false);
        if (gamepadBResult.Action == SaveSlotAction.Delete)
        {
            DeleteSaveSlot(gamepadBResult.Slot, isPauseScreen: true);
            return;
        }

        var confirmPressed = IsNewKeyPress(keyboard, Keys.Enter) ||
                             IsNewKeyPress(keyboard, Keys.Space) ||
                             IsNewButtonPress(gamePad, Buttons.A);

        if (IsMobile)
        {
            var slotResult = _pauseSlotInput.UpdateMobileTouch(
                elapsedSeconds,
                touchInputs,
                slot => ToUiRect(PauseSlotButton(slot)),
                SaveProgressStore.HasSlot);
            if (slotResult.Action == SaveSlotAction.Delete)
            {
                _pauseSelection = PauseSaveSlotStartIndex + slotResult.Slot - 1;
                DeleteSaveSlot(slotResult.Slot, isPauseScreen: true);
                return;
            }

            if (slotResult.Action == SaveSlotAction.Activate)
            {
                _pauseSelection = PauseSaveSlotStartIndex + slotResult.Slot - 1;
                SaveStateToSlot(slotResult.Slot);
                return;
            }

            if (!_pauseSlotInput.HoldingSlot.HasValue)
            {
                var tapPoint = GetFreshTouchPressPoint(touchInputs);
                if (tapPoint.HasValue && TryHandlePauseNonSlotClick(tapPoint.Value))
                    return;
            }

            if (confirmPressed)
                ActivatePauseSelection();
            return;
        }

        var desktopSlotResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot,
            deleteKeyPressed: IsNewKeyPress(keyboard, Keys.Delete),
            confirmPressed: confirmPressed,
            rightClickPoint: ToUiPointNullable(GetFreshRightClickPoint()),
            leftClickPoint: ToUiPointNullable(GetFreshLeftClickPoint()),
            getSlotBounds: slot => ToUiRect(PauseSlotButton(slot)));

        if (desktopSlotResult.Action == SaveSlotAction.Delete)
        {
            _pauseSelection = PauseSaveSlotStartIndex + desktopSlotResult.Slot - 1;
            DeleteSaveSlot(desktopSlotResult.Slot, isPauseScreen: true);
            return;
        }

        if (desktopSlotResult.Action == SaveSlotAction.Activate)
        {
            _pauseSelection = PauseSaveSlotStartIndex + desktopSlotResult.Slot - 1;
            SaveStateToSlot(desktopSlotResult.Slot);
            return;
        }

        var leftClick = GetFreshLeftClickPoint();
        if (leftClick.HasValue && TryHandlePauseNonSlotClick(leftClick.Value))
            return;

        if (confirmPressed)
            ActivatePauseSelection();
    }

    private int? GetSelectedPauseSlot() =>
        _pauseSelection >= PauseSaveSlotStartIndex &&
        _pauseSelection < PauseSaveSlotStartIndex + SaveProgressStore.SlotCount
            ? _pauseSelection - PauseSaveSlotStartIndex + 1
            : null;

    private bool TryHandlePauseNonSlotClick(Point point)
    {
        if (PauseContinueButton().Contains(point))
        {
            _pauseSelection = PauseContinueIndex;
            OpenSaveSlotSelection(GameScreen.Paused);
            return true;
        }

        if (PauseResumeButton().Contains(point))
        {
            _pauseSlotInput.Reset();
            _screen = GameScreen.Act01;
            return true;
        }

        if (PauseOptionsButton().Contains(point))
        {
            OpenOptions(GameScreen.Paused);
            return true;
        }

        if (PauseMainMenuButton().Contains(point))
        {
            LeaveToMainMenu();
            return true;
        }

        return false;
    }

    private const int PauseResumeIndex = 0;
    private const int PauseContinueIndex = 1;
    private const int PauseSaveSlotStartIndex = 2;
    private const int PauseOptionsIndex = PauseSaveSlotStartIndex + SaveProgressStore.SlotCount;
    private const int PauseMainMenuIndex = PauseOptionsIndex + 1;
    private const int PauseOptionCount = PauseMainMenuIndex + 1;

    private void ActivatePauseSelection()
    {
        if (_pauseSelection == PauseResumeIndex)
        {
            _screen = GameScreen.Act01;
            return;
        }

        if (_pauseSelection == PauseContinueIndex)
        {
            OpenSaveSlotSelection(GameScreen.Paused);
            return;
        }

        if (_pauseSelection >= PauseSaveSlotStartIndex &&
            _pauseSelection < PauseSaveSlotStartIndex + SaveProgressStore.SlotCount)
        {
            SaveStateToSlot(_pauseSelection - PauseSaveSlotStartIndex + 1);
            return;
        }

        if (_pauseSelection == PauseOptionsIndex)
        {
            OpenOptions(GameScreen.Paused);
            return;
        }

        if (_pauseSelection == PauseMainMenuIndex)
            LeaveToMainMenu();
    }

    private void SaveStateToSlot(int slot)
    {
        SaveCurrentProgress();
        SaveProgressStore.SaveSlot(slot, CloneSaveState(_saveProgress));
        _pauseStatusMessage = string.Format(
            CultureInfo.CurrentCulture,
            LocalizedText.Get("SaveComplete"),
            slot);
        _audio.PlayCue(AudioCue.Collectable, 0.7f);
    }

    private void DeleteSaveSlot(int slot, bool isPauseScreen)
    {
        string message;
        if (!SaveProgressStore.HasSlot(slot))
        {
            message = string.Format(
                CultureInfo.CurrentCulture,
                LocalizedText.Get("SlotUnavailable"),
                slot);
        }
        else if (SaveProgressStore.DeleteSlot(slot))
        {
            message = string.Format(
                CultureInfo.CurrentCulture,
                LocalizedText.Get("SlotDeleted"),
                slot);
            _audio?.PlayCue(AudioCue.UiClick, 0.7f);
        }
        else
        {
            message = string.Format(
                CultureInfo.CurrentCulture,
                LocalizedText.Get("SlotUnavailable"),
                slot);
        }

        if (isPauseScreen)
            _pauseStatusMessage = message;
        else
            _slotStatusMessage = message;
    }

    private void OpenSaveSlotSelection(GameScreen returnScreen)
    {
        _slotSelectionReturnScreen = returnScreen;
        _slotSelection = 0;
        _slotStatusMessage = string.Empty;
        _loadSlotInput.Reset();
        _screen = GameScreen.SaveSlotSelection;
    }

    private void UpdateSaveSlotSelection(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var touchInputs = ConvertTouchesToVirtual(touches);
        var optionCount = SaveProgressStore.SlotCount + 1;

        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewGamepadUp(gamePad))
            _slotSelection = (_slotSelection + optionCount - 1) % optionCount;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewGamepadDown(gamePad))
            _slotSelection = (_slotSelection + 1) % optionCount;

        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            ReturnFromSaveSlotSelection();
            return;
        }

        var selectedSlot = _slotSelection < SaveProgressStore.SlotCount
            ? _slotSelection + 1
            : (int?)null;

        var gamepadBResult = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot,
            gamepadBPressed: IsNewButtonPress(gamePad, Buttons.B),
            isSlotOccupied: SaveProgressStore.HasSlot,
            allowBackWhenNotDeleting: true);
        if (gamepadBResult.Action == SaveSlotAction.Delete)
        {
            DeleteSaveSlot(gamepadBResult.Slot, isPauseScreen: false);
            return;
        }

        if (gamepadBResult.Action == SaveSlotAction.Back)
        {
            ReturnFromSaveSlotSelection();
            return;
        }

        var confirmPressed = IsNewKeyPress(keyboard, Keys.Enter) ||
                             IsNewKeyPress(keyboard, Keys.Space) ||
                             IsNewButtonPress(gamePad, Buttons.A);

        if (IsMobile)
        {
            var slotResult = _loadSlotInput.UpdateMobileTouch(
                elapsedSeconds,
                touchInputs,
                slot => ToUiRect(SaveSlotButton(slot)),
                SaveProgressStore.HasSlot);
            if (slotResult.Action == SaveSlotAction.Delete)
            {
                _slotSelection = slotResult.Slot - 1;
                DeleteSaveSlot(slotResult.Slot, isPauseScreen: false);
                return;
            }

            if (slotResult.Action == SaveSlotAction.Activate)
            {
                _slotSelection = slotResult.Slot - 1;
                LoadStateFromSlot(slotResult.Slot);
                return;
            }

            if (!_loadSlotInput.HoldingSlot.HasValue)
            {
                var tapPoint = GetFreshTouchPressPoint(touchInputs);
                if (tapPoint.HasValue && SaveSlotBackButton().Contains(tapPoint.Value))
                {
                    ReturnFromSaveSlotSelection();
                    return;
                }
            }

            if (confirmPressed)
            {
                if (selectedSlot.HasValue)
                    LoadStateFromSlot(selectedSlot.Value);
                else
                    ReturnFromSaveSlotSelection();
            }

            return;
        }

        var desktopSlotResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot,
            deleteKeyPressed: IsNewKeyPress(keyboard, Keys.Delete),
            confirmPressed: confirmPressed,
            rightClickPoint: ToUiPointNullable(GetFreshRightClickPoint()),
            leftClickPoint: ToUiPointNullable(GetFreshLeftClickPoint()),
            getSlotBounds: slot => ToUiRect(SaveSlotButton(slot)));

        if (desktopSlotResult.Action == SaveSlotAction.Delete)
        {
            _slotSelection = desktopSlotResult.Slot - 1;
            DeleteSaveSlot(desktopSlotResult.Slot, isPauseScreen: false);
            return;
        }

        if (desktopSlotResult.Action == SaveSlotAction.Activate)
        {
            _slotSelection = desktopSlotResult.Slot - 1;
            LoadStateFromSlot(desktopSlotResult.Slot);
            return;
        }

        var leftClick = GetFreshLeftClickPoint();
        if (leftClick.HasValue && SaveSlotBackButton().Contains(leftClick.Value))
        {
            ReturnFromSaveSlotSelection();
            return;
        }

        if (confirmPressed && !selectedSlot.HasValue)
            ReturnFromSaveSlotSelection();
    }

    private void ReturnFromSaveSlotSelection()
    {
        _loadSlotInput.Reset();
        _screen = _slotSelectionReturnScreen;
        _slotStatusMessage = string.Empty;
    }

    private void LoadStateFromSlot(int slot)
    {
        if (!SaveProgressStore.HasSlot(slot))
        {
            _slotStatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                LocalizedText.Get("SlotUnavailable"),
                slot);
            return;
        }

        _saveProgress = SaveProgressStore.LoadSlot(slot);
        _livesRemaining = Math.Clamp(_saveProgress.LivesRemaining, 1, MaximumLives);
        _selectedDifficulty = Enum.IsDefined(typeof(GameDifficulty), _saveProgress.Difficulty)
            ? _saveProgress.Difficulty
            : GameDifficulty.Normal;
        _difficultySelection = (int)_selectedDifficulty;

        var savedCharacterIndex = Array.IndexOf(_characterIds, _saveProgress.CharacterId);
        if (savedCharacterIndex >= 0)
            SelectCharacter(savedCharacterIndex);

        var levelId = ResolveSavedLevelId(_saveProgress);
        LoadLevel(levelId);
        StartAct01(levelId);
    }

    private static string ResolveSavedLevelId(SaveProgressState state)
    {
        var candidates = new[] { state.CurrentLevelId, state.CheckpointLevelId, state.UnlockedLevelId };
        foreach (var candidate in candidates)
        {
            switch (candidate)
            {
                case "Act_03_C":
                case "Act_03_B":
                case "Act_03_A":
                case "Act_02_C":
                case "Act_02_B":
                case "Act_02_A":
                case "Act_01_C":
                case "Act_01_B":
                case "Act_01_A":
                    return candidate;
            }
        }

        return "Act_01_A";
    }

    private static SaveProgressState CloneSaveState(SaveProgressState source) => new()
    {
        CurrentLevelId = source.CurrentLevelId,
        UnlockedLevelId = source.UnlockedLevelId,
        CharacterId = source.CharacterId,
        Difficulty = source.Difficulty,
        LivesRemaining = source.LivesRemaining,
        SealAshesParts = source.SealAshesParts,
        Act03Seal3RestorationApplied = source.Act03Seal3RestorationApplied,
        Act03FinalBossesCompleted = source.Act03FinalBossesCompleted,
        CheckpointLevelId = source.CheckpointLevelId,
        CheckpointId = source.CheckpointId
    };

    private void OpenOptions(GameScreen returnScreen)
    {
        _optionsReturnScreen = returnScreen;
        _optionsSelection = LanguageOptionIndex;
        _languageSwipeDetector.Reset();
        _screen = GameScreen.Options;
    }

    private void ReturnFromOptions()
    {
        _languageSwipeDetector.Reset();
        _screen = _optionsReturnScreen;
    }

    private void LeaveToMainMenu()
    {
        _screen = GameScreen.MainMenu;
        _audio.PlayMenuMusic();
    }

    private void UpdateIntro(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        _introScroll += (float)gameTime.ElapsedGameTime.TotalSeconds * IntroScrollSpeed;
        var introLineCount = GetIntroLayoutLines().Count;
        var introFinished = _introScroll >= VirtualHeight + introLineCount * IntroLineHeight + 24f;
        var skipRequested = IsNewKeyPress(keyboard, Keys.Enter) ||
                            IsNewKeyPress(keyboard, Keys.Space) ||
                            IsNewButtonPress(gamePad, Buttons.A) ||
                            GetPressedPoint(touches).HasValue;

        if (introFinished || skipRequested)
        {
            _introScroll = 0f;
            _screen = GameScreen.MainMenu;
            _audio.PlayMenuMusic();
        }
    }

    private void ActivateMainMenuSelection()
    {
        _audio.PlayCue(AudioCue.UiClick, 0.65f);
        switch (_menuSelection)
        {
            case 0:
                _screen = GameScreen.PlayerSelection;
                break;
            case 1:
                OpenSaveSlotSelection(GameScreen.MainMenu);
                break;
            case 2:
                OpenOptions(GameScreen.MainMenu);
                break;
            case 3:
                Exit();
                break;
        }
    }

    private void UpdateOptions(KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        if (IsDesktop && IsNewKeyPress(keyboard, Keys.Tab))
        {
            _optionsSelection = ControlsOptionIndex;
            _screen = GameScreen.Controls;
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewGamepadUp(gamePad))
            MoveOptionsSelection(-1);
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewGamepadDown(gamePad))
            MoveOptionsSelection(1);
        if (IsNewKeyPress(keyboard, Keys.Left) || IsNewGamepadLeft(gamePad))
            AdjustSelectedOption(-1);
        if (IsNewKeyPress(keyboard, Keys.Right) || IsNewGamepadRight(gamePad))
            AdjustSelectedOption(1);

        var touchInputs = ConvertTouchesToVirtual(touches);
        Point? point = null;
        if (IsMobile)
        {
            var swipeDirection = _languageSwipeDetector.Update(
                touchInputs,
                ToUiRect(LanguageSelectorSwipeArea()),
                out var swipeConsumed,
                out var tapReleasedPoint);
            if (swipeDirection != 0)
            {
                _optionsSelection = LanguageOptionIndex;
                CycleLanguage(swipeDirection);
                return;
            }

            if (tapReleasedPoint.HasValue)
                point = new Point(tapReleasedPoint.Value.X, tapReleasedPoint.Value.Y);
            else if (!swipeConsumed)
                point = GetFreshTouchPressPoint(touchInputs);
        }
        else
        {
            point = GetFreshLeftClickPoint();
        }

        if (point.HasValue)
        {
            if (LanguagePreviousArrowButton().Contains(point.Value))
            {
                _optionsSelection = LanguageOptionIndex;
                CycleLanguage(-1);
                return;
            }

            if (LanguageNextArrowButton().Contains(point.Value))
            {
                _optionsSelection = LanguageOptionIndex;
                CycleLanguage(1);
                return;
            }

            if (LanguageSelectorBounds().Contains(point.Value))
            {
                _optionsSelection = LanguageOptionIndex;
                return;
            }

            if (IsMobile && OptionsBackButton().Contains(point.Value))
            {
                ReturnFromOptions();
                return;
            }

            if (BgmVolumeButton().Contains(point.Value))
            {
                _optionsSelection = BgmVolumeOptionIndex;
                SetAudioVolumeFromPoint(BgmVolumeButton(), point.Value, bgm: true);
                return;
            }

            if (SfxVolumeButton().Contains(point.Value))
            {
                _optionsSelection = SfxVolumeOptionIndex;
                SetAudioVolumeFromPoint(SfxVolumeButton(), point.Value, bgm: false);
                return;
            }

            if (MuteButton().Contains(point.Value))
            {
                _optionsSelection = MuteOptionIndex;
                _audio.ToggleMute();
                SaveCurrentSettings();
                return;
            }

            if (IsDesktop && ControlsButton().Contains(point.Value))
            {
                _optionsSelection = ControlsOptionIndex;
                _screen = GameScreen.Controls;
                return;
            }

            if (IsDesktop && FullScreenButton().Contains(point.Value))
            {
                _optionsSelection = FullScreenOptionIndex;
                ToggleFullscreen();
                return;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            ActivateOptionsSelection();
        if (IsNewButtonPress(gamePad, Buttons.B))
            ReturnFromOptions();
    }

    private const int LanguageOptionIndex = 0;

    private int ControlsOptionIndex => 1;

    private int FullScreenOptionIndex => 2;

    private int BgmVolumeOptionIndex => IsDesktop ? 3 : 1;

    private int SfxVolumeOptionIndex => BgmVolumeOptionIndex + 1;

    private int MuteOptionIndex => SfxVolumeOptionIndex + 1;

    private int BackOptionIndex => MuteOptionIndex + (IsMobile ? 1 : 0);

    private int OptionsOptionCount => BackOptionIndex + 1;

    private void MoveOptionsSelection(int direction)
    {
        var optionCount = OptionsOptionCount;
        _optionsSelection = (_optionsSelection + direction + optionCount) % optionCount;
    }

    private void AdjustSelectedOption(int direction)
    {
        const float step = 0.05f;
        if (_optionsSelection == LanguageOptionIndex)
        {
            CycleLanguage(direction);
            return;
        }

        if (IsDesktop && _optionsSelection == FullScreenOptionIndex)
        {
            ToggleFullscreen();
            return;
        }

        if (_optionsSelection == BgmVolumeOptionIndex)
        {
            _audio.SetBgmVolume(_audio.BgmVolume + direction * step);
            SaveCurrentSettings();
        }
        else if (_optionsSelection == SfxVolumeOptionIndex)
        {
            _audio.SetSfxVolume(_audio.SfxVolume + direction * step);
            SaveCurrentSettings();
        }
        else if (_optionsSelection == MuteOptionIndex)
        {
            _audio.ToggleMute();
            SaveCurrentSettings();
        }
    }

    private void SetAudioVolumeFromPoint(Rectangle bounds, Point point, bool bgm)
    {
        var volume = Math.Clamp((point.X - bounds.Left) / (float)bounds.Width, 0f, 1f);
        if (bgm)
            _audio.SetBgmVolume(volume);
        else
            _audio.SetSfxVolume(volume);

        SaveCurrentSettings();
    }

    private void ActivateOptionsSelection()
    {
        if (_optionsSelection == LanguageOptionIndex)
        {
            CycleLanguage(1);
            return;
        }

        _audio.PlayCue(AudioCue.UiClick, 0.65f);

        if (IsDesktop && _optionsSelection == ControlsOptionIndex)
        {
            _screen = GameScreen.Controls;
            return;
        }

        if (IsDesktop && _optionsSelection == FullScreenOptionIndex)
        {
            ToggleFullscreen();
            return;
        }

        if (_optionsSelection == MuteOptionIndex)
        {
            _audio.ToggleMute();
            SaveCurrentSettings();
            return;
        }

        if (_optionsSelection == BackOptionIndex)
            ReturnFromOptions();
    }

    private void UpdateControls(KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        if (_waitingForBinding)
        {
            CaptureBinding(keyboard, gamePad);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewGamepadUp(gamePad))
            _controlSelection = (_controlSelection + _controlKeys.Length - 1) % _controlKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewGamepadDown(gamePad))
            _controlSelection = (_controlSelection + 1) % _controlKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Left) || IsNewKeyPress(keyboard, Keys.Right) || IsNewGamepadLeft(gamePad) || IsNewGamepadRight(gamePad))
            _bindingDevice = _bindingDevice == BindingDevice.Keyboard ? BindingDevice.Gamepad : BindingDevice.Keyboard;

        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            if (ControlsKeyboardButton().Contains(point.Value))
            {
                _bindingDevice = BindingDevice.Keyboard;
                return;
            }

            if (ControlsGamepadButton().Contains(point.Value))
            {
                _bindingDevice = BindingDevice.Gamepad;
                return;
            }

            for (var index = 0; index < _controlKeys.Length; index++)
            {
                if (ControlRow(index).Contains(point.Value))
                {
                    _controlSelection = index;
                    BeginBinding(keyboard, gamePad);
                    return;
                }
            }

            if (IsMobile && ControlsBackButton().Contains(point.Value))
            {
                _screen = GameScreen.Options;
                return;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            BeginBinding(keyboard, gamePad);
        if (IsNewButtonPress(gamePad, Buttons.B))
            _screen = GameScreen.Options;
    }

    private void BeginBinding(KeyboardState keyboard, GamePadState gamePad)
    {
        _waitingForBinding = true;
        _bindingReady = false;
        _ignoredBindingKeys = new HashSet<Keys>(keyboard.GetPressedKeys());
        _ignoredBindingButtons = new HashSet<Buttons>();
        foreach (var button in ConfigurableGamepadButtons)
        {
            if (gamePad.IsButtonDown(button))
                _ignoredBindingButtons.Add(button);
        }
    }

    private void CaptureBinding(KeyboardState keyboard, GamePadState gamePad)
    {
        if (!_bindingReady)
        {
            var keysReleased = true;
            foreach (var key in _ignoredBindingKeys)
            {
                if (keyboard.IsKeyDown(key))
                {
                    keysReleased = false;
                    break;
                }
            }

            var buttonsReleased = true;
            foreach (var button in _ignoredBindingButtons)
            {
                if (gamePad.IsButtonDown(button))
                {
                    buttonsReleased = false;
                    break;
                }
            }

            _bindingReady = keysReleased && buttonsReleased;
            return;
        }

        if (_bindingDevice == BindingDevice.Keyboard)
        {
            foreach (var key in keyboard.GetPressedKeys())
            {
                if (key == Keys.Escape)
                {
                    _waitingForBinding = false;
                    return;
                }

                _keyboardBindings[_controlSelection] = key;
                _waitingForBinding = false;
                return;
            }
        }
        else
        {
            foreach (var button in ConfigurableGamepadButtons)
            {
                if (gamePad.IsButtonDown(button))
                {
                    _gamepadBindings[_controlSelection] = button;
                    _waitingForBinding = false;
                    return;
                }
            }
        }
    }

    private void UpdatePlayerSelection(KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Left) || IsNewGamepadLeft(gamePad))
            SelectCharacter(_characterSelection - 1);
        if (IsNewKeyPress(keyboard, Keys.Right) || IsNewGamepadRight(gamePad))
            SelectCharacter(_characterSelection + 1);

        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            for (var index = 0; index < _characterIds.Length; index++)
            {
                if (CharacterSelectionButton(index).Contains(point.Value))
                {
                    SelectCharacter(index);
                    return;
                }
            }

            if (SelectionContinueButton().Contains(point.Value))
            {
                OpenDifficultySelection();
                return;
            }

            if (IsMobile && SelectionBackButton().Contains(point.Value))
            {
                _screen = GameScreen.MainMenu;
                return;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            OpenDifficultySelection();
        if (IsNewButtonPress(gamePad, Buttons.B))
            _screen = GameScreen.MainMenu;
    }

    private void OpenDifficultySelection()
    {
        _difficultySelection = (int)_selectedDifficulty;
        _screen = GameScreen.DifficultySelection;
    }

    private void UpdateDifficultySelection(KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewKeyPress(keyboard, Keys.Left) || IsNewGamepadUp(gamePad) || IsNewGamepadLeft(gamePad))
            _difficultySelection = (_difficultySelection + _difficultyKeys.Length - 1) % _difficultyKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewKeyPress(keyboard, Keys.Right) || IsNewGamepadDown(gamePad) || IsNewGamepadRight(gamePad))
            _difficultySelection = (_difficultySelection + 1) % _difficultyKeys.Length;

        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            for (var index = 0; index < _difficultyKeys.Length; index++)
            {
                if (DifficultyButton(index).Contains(point.Value))
                {
                    _difficultySelection = index;
                    return;
                }
            }

            if (SelectionContinueButton().Contains(point.Value))
            {
                ConfirmDifficulty();
                return;
            }

            if (IsMobile && SelectionBackButton().Contains(point.Value))
            {
                _screen = GameScreen.PlayerSelection;
                return;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            ConfirmDifficulty();
        if (IsNewButtonPress(gamePad, Buttons.B))
            _screen = GameScreen.PlayerSelection;
    }

    private void ConfirmDifficulty()
    {
        _selectedDifficulty = (GameDifficulty)_difficultySelection;
        _livesRemaining = StartingLives;
        _saveProgress.CheckpointLevelId = "Act_01_A";
        _saveProgress.CheckpointId = 0;
        _saveProgress.UnlockedLevelId = "Act_01_A";
        _saveProgress.SealAshesParts = 0;
        _saveProgress.Act03Seal3RestorationApplied = false;
        _saveProgress.Act03FinalBossesCompleted = false;
        LoadLevel("Act_01_A");
        StartAct01("Act_01_A");
    }

    private void CycleLanguage(int direction)
    {
        _languageSelector.Move(direction);
        LocalizationManager.SetCulture(_languageSelector.SelectedCode);
        _cachedIntroCulture = string.Empty;
        SaveCurrentSettings();
        _audio?.PlayCue(AudioCue.UiClick, 0.65f);
    }

    private void SelectCharacter(int index)
    {
        _characterSelection = (index + _characterIds.Length) % _characterIds.Length;
        if (_player is not null)
            _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
    }

    private void ContinueSavedProgress()
    {
        _selectedDifficulty = _saveProgress.Difficulty;
        _difficultySelection = (int)_selectedDifficulty;
        var savedCharacterIndex = Array.IndexOf(_characterIds, _saveProgress.CharacterId);
        if (savedCharacterIndex >= 0)
            SelectCharacter(savedCharacterIndex);

        var levelId = _saveProgress.UnlockedLevelId switch
        {
            "Act_03_C" => "Act_03_C",
            "Act_03_B" => "Act_03_B",
            "Act_03_A" => "Act_03_A",
            "Act_02_C" => "Act_02_C",
            "Act_02_B" => "Act_02_B",
            "Act_02_A" => "Act_02_A",
            "Act_01_C" => "Act_01_C",
            "Act_01_B" => "Act_01_B",
            _ => "Act_01_A"
        };
        LoadLevel(levelId);
        StartAct01(levelId);
    }

    private void StartAct01(string levelId)
    {
        _screen = GameScreen.Act01;
        _currentLevelId = levelId;
        _levelBriefingRemaining = LevelBriefingDuration;
        _atmosphereTime = 0f;
        _collectedCollectibles.Clear();
        _finalBossDefeated = false;
        _finalBossRewardCollected = false;
        _bossBannerText = string.Empty;
        _bossBannerRemaining = 0f;
        _audio.PlayActMusic(_currentLevelId);
        InitializeAct03BossState();
        InitializeCheckpoint(levelId);
        _playerPosition = _checkpointPosition;
        _moveInput = Vector2.Zero;
        _analogVisualInput = Vector2.Zero;
        _verticalVelocity = 0f;
        _isGrounded = true;
        _jumpsUsed = 0;
        _runRemaining = 0f;
        _stamina = StaminaMaximum;
        _power = 0f;
        _powerRecoveryElapsed = 0f;
        _projectiles.Clear();
        _enemyProjectiles.Clear();
        _playerHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
        _playerInvulnerability = 0f;
        _playerKnockbackVelocity = 0f;
        _playerFacing = 1;
        _playerAttackHitApplied = false;
        _player.Reset();
        foreach (var enemy in _enemies)
            enemy.Reset();
        _boss?.Reset();
        _effects.Update(10f);
        SaveCurrentProgress();
        UpdateCamera();
    }

    private void InitializeCheckpoint(string levelId)
    {
        _checkpointId = 0;
        _checkpointPosition = _level.PlayerSpawn;

        if (!string.Equals(_saveProgress.CheckpointLevelId, levelId, StringComparison.OrdinalIgnoreCase) ||
            _saveProgress.CheckpointId <= 0)
            return;

        foreach (var checkpoint in _level.Checkpoints)
        {
            if (!TryGetIntProperty(checkpoint, "CheckpointId", out var checkpointId) ||
                checkpointId != _saveProgress.CheckpointId)
                continue;

            _checkpointId = checkpointId;
            _checkpointPosition = ReadRespawnPosition(checkpoint);
            return;
        }
    }

    private void CaptureCheckpoint()
    {
        foreach (var checkpoint in _level.Checkpoints)
        {
            if (!PlayerHitbox.Intersects(checkpoint.Bounds) ||
                !TryGetIntProperty(checkpoint, "CheckpointId", out var checkpointId) ||
                checkpointId <= _checkpointId)
                continue;

            _checkpointId = checkpointId;
            _checkpointPosition = ReadRespawnPosition(checkpoint);
            _saveProgress.CheckpointLevelId = _currentLevelId;
            _saveProgress.CheckpointId = _checkpointId;
            SaveCurrentProgress();
        }
    }

    private Vector2 ReadRespawnPosition(LevelObjectData checkpoint)
    {
        var x = TryGetIntProperty(checkpoint, "RespawnX", out var respawnX)
            ? respawnX
            : checkpoint.Bounds.X;
        var y = TryGetIntProperty(checkpoint, "RespawnY", out var respawnY)
            ? respawnY
            : checkpoint.Bounds.Bottom;
        return new Vector2(x, y);
    }

    private static bool TryGetIntProperty(LevelObjectData item, string name, out int value)
    {
        value = 0;
        return item.Properties.TryGetValue(name, out var text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

#if DEBUG
    private IReadOnlyList<DebugCheatCodeSystem.CheckpointOption> GetDebugCheckpointOptions()
    {
        var checkpoints = new List<DebugCheatCodeSystem.CheckpointOption>();
        foreach (var checkpoint in _level.Checkpoints)
        {
            if (!TryGetIntProperty(checkpoint, "CheckpointId", out var checkpointId))
                continue;

            checkpoints.Add(new DebugCheatCodeSystem.CheckpointOption(
                checkpointId,
                string.IsNullOrWhiteSpace(checkpoint.Name) ? $"Checkpoint {checkpointId}" : checkpoint.Name,
                ReadRespawnPosition(checkpoint)));
        }

        foreach (var bossSpawn in _level.BossSpawns)
        {
            var bossPosition = new Vector2(
                Math.Max(32f, bossSpawn.Bounds.Center.X - 48f),
                bossSpawn.Bounds.Bottom);
            checkpoints.Add(new DebugCheatCodeSystem.CheckpointOption(
                int.MaxValue,
                "Final boss / end of level",
                bossPosition));
            break;
        }

        checkpoints.Sort((left, right) => left.Id.CompareTo(right.Id));
        return checkpoints;
    }

    private void ApplyDebugCheatEffects()
    {
        if (_debugCheatCodes.TryConsumeRestoreResources())
        {
            _playerHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
            _stamina = StaminaMaximum;
            _power = CombatBalance.PlayerPowerMaximum;
            _livesRemaining = MaximumLives;
        }

        if (_debugCheatCodes.TryConsumeCheckpoint(out var checkpoint))
        {
            _checkpointId = checkpoint.Id;
            _checkpointPosition = checkpoint.RespawnPosition;
            _playerPosition = checkpoint.RespawnPosition;
            _verticalVelocity = 0f;
            _playerKnockbackVelocity = 0f;
            _isGrounded = true;
            _jumpsUsed = 0;
            _runRemaining = 0f;
            _playerInvulnerability = 1f;
            _projectiles.Clear();
            _enemyProjectiles.Clear();
            _player.Reset();
            SaveCurrentProgress();
            UpdateCamera();
        }

        if (_debugCheatCodes.InfiniteHealth)
            _playerHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
        if (_debugCheatCodes.InfiniteStamina)
            _stamina = StaminaMaximum;
        if (_debugCheatCodes.InfinitePower)
            _power = CombatBalance.PlayerPowerMaximum;
        if (_debugCheatCodes.InfiniteLives)
            _livesRemaining = MaximumLives;
    }

    private bool UpdateMobileDebugLongPress(GameTime gameTime, TouchCollection touches)
    {
#if DEBUG
        if (!IsMobile || _screen != GameScreen.Paused)
        {
            ResetMobileDebugLongPress();
            return false;
        }

        TouchLocation? targetTouch = null;
        foreach (var touch in touches)
        {
            if (touch.State is not (TouchLocationState.Pressed or TouchLocationState.Moved))
                continue;

            if (PauseResumeButton().Contains(ToVirtualPoint(touch.Position)))
            {
                targetTouch = touch;
                break;
            }
        }

        if (!targetTouch.HasValue)
        {
            var wasTracking = _mobileCheatLongPressTouchId >= 0;
            var wasTriggered = _mobileCheatLongPressTriggered;
            ResetMobileDebugLongPress();
            return wasTracking && wasTriggered;
        }

        if (_mobileCheatLongPressTouchId != targetTouch.Value.Id)
        {
            _mobileCheatLongPressTouchId = targetTouch.Value.Id;
            _mobileCheatLongPressElapsed = 0f;
            _mobileCheatLongPressTriggered = false;
        }

        _mobileCheatLongPressElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (!_mobileCheatLongPressTriggered &&
            _mobileCheatLongPressElapsed >= MobileCheatLongPressDuration)
        {
            _mobileCheatLongPressTriggered = true;
            _debugCheatCodes.Open();
        }

        return true;
#else
        return false;
#endif
    }

    private bool UpdateMobileDebugLongPressState(TouchCollection touches)
    {
#if DEBUG
        if (!IsMobile || _mobileCheatLongPressTouchId < 0)
            return false;

        foreach (var touch in touches)
        {
            if (touch.Id == _mobileCheatLongPressTouchId &&
                touch.State is TouchLocationState.Pressed or TouchLocationState.Moved)
                return true;
        }
#endif
        return false;
    }

    private void ResetMobileDebugLongPress()
    {
#if DEBUG
        _mobileCheatLongPressTouchId = -1;
        _mobileCheatLongPressElapsed = 0f;
        _mobileCheatLongPressTriggered = false;
#endif
    }
#endif

    private void LoadLevel(string levelId)
    {
        _currentLevelId = levelId;
        _level = GameLevel.Load(GraphicsDevice, LevelDefinition.ForId(levelId));
        _level.ConfigureDifficulty(_selectedDifficulty);
        LoadEnemies();
        _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
    }

    private void LoadEnemies()
    {
        _enemies.Clear();
        _pendingEnemySpawns.Clear();
        _boss = null;
        _act03BossSpawn = null;
        foreach (var spawn in _level.EnemySpawns)
        {
            var enemyType = spawn.Properties.TryGetValue("EnemyType", out var configuredType)
                ? configuredType
                : string.Empty;
            if (spawn.Properties.TryGetValue("SpawnOnly", out var spawnOnly) &&
                bool.TryParse(spawnOnly, out var isSpawnOnly) && isSpawnOnly)
            {
                _pendingEnemySpawns.Add(spawn);
                continue;
            }

            if (string.Equals(enemyType, "PendingGroup", StringComparison.OrdinalIgnoreCase))
            {
                LoadEnemyGroup(spawn);
                continue;
            }

            CreateEnemy(spawn, enemyType);
        }

        foreach (var spawn in _level.BossSpawns)
        {
            if (string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
            {
                _act03BossSpawn = spawn;
                var minotaur = new BossInstance(Content, BossKind.Minotaur, spawn, "Minotaur_3");
                minotaur.ConfigureDifficulty(_selectedDifficulty);
                _boss = minotaur;
                break;
            }

            if (BossInstance.TryCreate(Content, spawn, out var boss))
            {
                boss.ConfigureDifficulty(_selectedDifficulty);
                _boss = boss;
                break;
            }
        }
    }

    private void InitializeAct03BossState()
    {
        _act03Encounter.InitializeForLevel(_currentLevelId, _saveProgress);
        if (_act03Encounter.Stage == Act03BossStage.Completed)
        {
            _boss = null;
            _finalBossDefeated = true;
            _finalBossRewardCollected = true;
            return;
        }

        if (_act03BossSpawn is null)
            return;

        if (_act03Encounter.Stage == Act03BossStage.Minotaur3)
        {
            if (_boss is null ||
                _boss.Kind != BossKind.Minotaur ||
                !string.Equals(_boss.VariantId, "Minotaur_3", StringComparison.Ordinal))
            {
                _boss = new BossInstance(Content, BossKind.Minotaur, _act03BossSpawn, "Minotaur_3");
                _boss.ConfigureDifficulty(_selectedDifficulty);
            }
            return;
        }

        if (_act03Encounter.Stage == Act03BossStage.FireWizard)
        {
            // Seal 3/4 was already collected and restored resources in this save;
            // resume at Fire Wizard without re-awarding Ash Seal 3/4 or restoring resources again.
            SpawnFinalSequenceBoss("Fire Wizard");
        }
    }

    private void SpawnFinalSequenceBoss(string variantId)
    {
        if (_act03BossSpawn is null)
            return;

        _enemyProjectiles.Clear();
        _boss = new BossInstance(Content, BossKind.Wizard, _act03BossSpawn, variantId);
        _boss.ConfigureDifficulty(_selectedDifficulty);
        _audio.PlayFinalBossMusic(variantId);
        _effects.Spawn(_boss.Position - new Vector2(0f, 30f), CombatEffectKind.Magic, Vector2.UnitY, 1.5f);
        ShowBossBanner(LocalizedText.Get(_boss.LocalizationNameKey));
    }

    private void ShowBossBanner(string message)
    {
        _bossBannerText = message;
        _bossBannerRemaining = BossBannerDuration;
    }

    private void ActivatePendingEnemySpawns()
    {
        for (var index = _pendingEnemySpawns.Count - 1; index >= 0; index--)
        {
            var spawn = _pendingEnemySpawns[index];
            if (Math.Abs(_playerPosition.X - spawn.Bounds.Center.X) > EnemySpawnActivationDistance)
                continue;

            _pendingEnemySpawns.RemoveAt(index);
            var enemyType = spawn.Properties.TryGetValue("EnemyType", out var configuredType)
                ? configuredType
                : string.Empty;
            if (string.Equals(enemyType, "PendingGroup", StringComparison.OrdinalIgnoreCase))
                LoadEnemyGroup(spawn);
            else
                CreateEnemy(spawn, enemyType);
        }
    }

    private void CreateEnemy(LevelObjectData spawn, string enemyType)
    {
        if (!EnemyCharacter.IsSupported(enemyType) || EnemyCharacter.IsBossType(enemyType))
            return;

        var enemy = new EnemyInstance(EnemyCharacter.Load(Content, enemyType), spawn);
        enemy.ConfigureDifficulty(_selectedDifficulty);
        _enemies.Add(enemy);
    }

    private void LoadEnemyGroup(LevelObjectData groupSpawn)
    {
        var groupCount = TryGetIntProperty(groupSpawn, "GroupCount", out var configuredCount)
            ? Math.Clamp(configuredCount, 1, 12)
            : 1;
        var groupEnemyType = groupSpawn.Properties.TryGetValue("GroupEnemyType", out var configuredType) &&
                             EnemyCharacter.IsSupported(configuredType) &&
                             !EnemyCharacter.IsBossType(configuredType)
            ? configuredType
            : "Skeleton_Warrior";

        for (var index = 0; index < groupCount; index++)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in groupSpawn.Properties)
                properties[pair.Key] = pair.Value;
            properties["EnemyType"] = groupEnemyType;
            var spawn = new LevelObjectData
            {
                Name = $"{groupSpawn.Name}_{index + 1}",
                Type = "EnemySpawn",
                Bounds = new Rectangle(groupSpawn.Bounds.X + index * 28, groupSpawn.Bounds.Y, groupSpawn.Bounds.Width, groupSpawn.Bounds.Height),
                Properties = properties
            };
            var enemy = new EnemyInstance(EnemyCharacter.Load(Content, groupEnemyType), spawn);
            enemy.ConfigureDifficulty(_selectedDifficulty);
            _enemies.Add(enemy);
        }
    }

    private void UpdateAct01(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _atmosphereTime += elapsedSeconds;
        _act03Encounter.Update(elapsedSeconds);
        if (_bossBannerRemaining > 0f)
            _bossBannerRemaining = Math.Max(0f, _bossBannerRemaining - elapsedSeconds);
        _effects.Update(elapsedSeconds);
        if (_levelBriefingRemaining > 0f)
        {
            _levelBriefingRemaining = Math.Max(0f, _levelBriefingRemaining - elapsedSeconds);
            if (IsNewKeyPress(keyboard, Keys.Enter) ||
                IsNewKeyPress(keyboard, Keys.Space) ||
                IsNewButtonPress(gamePad, Buttons.A) ||
                GetPressedPoint(touches).HasValue)
                _levelBriefingRemaining = 0f;
        }

        if (_player.Animation == PlayerAnimationState.Dead)
        {
            _player.Update(elapsedSeconds);
            if (_player.IsDeadAnimationComplete)
                ResetPlayerAfterDefeat();
            return;
        }

        _playerInvulnerability = Math.Max(0f, _playerInvulnerability - elapsedSeconds);
        if (_runRemaining <= 0f)
            _stamina = Math.Min(StaminaMaximum, _stamina + StaminaRecoveryPerSecond * elapsedSeconds);
        UpdatePlayerPowerRecovery(elapsedSeconds);
        _moveInput = Vector2.Zero;
        _analogVisualInput = Vector2.Zero;
        if (IsKeyboardMoveDown(keyboard, 0))
            _moveInput.X -= 1f;
        if (IsKeyboardMoveDown(keyboard, 1))
            _moveInput.X += 1f;
        if (gamePad.IsButtonDown(_gamepadBindings[0]))
            _moveInput.X -= 1f;
        if (gamePad.IsButtonDown(_gamepadBindings[1]))
            _moveInput.X += 1f;
        if (gamePad.ThumbSticks.Left.Length() > 0.2f)
            _moveInput.X = gamePad.ThumbSticks.Left.X;

        if (IsDesktop)
        {
            if (IsNewKeyPress(keyboard, _keyboardBindings[2]) || IsNewButtonPress(gamePad, _gamepadBindings[2]))
                StartAction(PlayerAnimationState.Attack1);
            else if (IsNewKeyPress(keyboard, _keyboardBindings[3]) || IsNewButtonPress(gamePad, _gamepadBindings[3]))
                StartAction(PlayerAnimationState.Attack2);
            else if (IsNewKeyPress(keyboard, _keyboardBindings[4]) || IsNewButtonPress(gamePad, _gamepadBindings[4]))
                TryJump();
            else if (IsNewKeyPress(keyboard, _keyboardBindings[5]) || IsNewButtonPress(gamePad, _gamepadBindings[5]))
                StartAction(PlayerAnimationState.Run);
            else if (IsNewKeyPress(keyboard, _keyboardBindings[6]) || IsNewButtonPress(gamePad, _gamepadBindings[6]))
                StartPrimarySpecial();
            else if (IsNewKeyPress(keyboard, _keyboardBindings[7]) || IsNewButtonPress(gamePad, _gamepadBindings[7]))
                StartSecondarySpecial();
        }

        foreach (var touch in touches)
        {
            if (!IsMobile || touch.State is not (TouchLocationState.Pressed or TouchLocationState.Moved))
                continue;

            var point = ToVirtualPoint(touch.Position);
            var analogCenter = AnalogCenter();
            var analogRadius = AnalogRadius;
            if (Vector2.Distance(point.ToVector2(), analogCenter) <= analogRadius + 12f)
            {
                var analogInput = new Vector2(
                    (point.X - analogCenter.X) / analogRadius,
                    (point.Y - analogCenter.Y) / analogRadius);
                if (analogInput.LengthSquared() > 1f)
                    analogInput.Normalize();
                _analogVisualInput = analogInput;
                _moveInput = analogInput;
            }

            if (touch.State == TouchLocationState.Pressed)
            {
                if (ActionButtonBounds(0).Contains(point))
                    StartAction(PlayerAnimationState.Attack1);
                else if (ActionButtonBounds(1).Contains(point))
                    StartAction(PlayerAnimationState.Attack2);
                else if (ActionButtonBounds(2).Contains(point))
                    TryJump();
                else if (ActionButtonBounds(3).Contains(point))
                    StartSecondarySpecial();
                else if (ActionButtonBounds(4).Contains(point))
                    StartPrimarySpecial();
                else if (ActionButtonBounds(5).Contains(point))
                    StartAction(PlayerAnimationState.Run);
            }
        }

        var mouse = Mouse.GetState();
        if (IsMobile && mouse.LeftButton == ButtonState.Pressed)
        {
            var point = ToVirtualPoint(mouse.Position.ToVector2());
            var analogCenter = AnalogCenter();
            if (Vector2.Distance(point.ToVector2(), analogCenter) <= AnalogRadius + 12f)
            {
                var analogInput = new Vector2(
                    (point.X - analogCenter.X) / AnalogRadius,
                    (point.Y - analogCenter.Y) / AnalogRadius);
                if (analogInput.LengthSquared() > 1f)
                    analogInput.Normalize();
                _analogVisualInput = analogInput;
                _moveInput = analogInput;
            }
        }

        if (_moveInput.LengthSquared() > 1f)
            _moveInput.Normalize();
        if (Math.Abs(_moveInput.X) > 0.05f)
            _playerFacing = _moveInput.X < 0f ? -1 : 1;
        var movementSpeed = _runRemaining > 0f ? RunSpeed : PlayerSpeed;
        _playerPosition.X += (_moveInput.X * movementSpeed + _playerKnockbackVelocity) * elapsedSeconds;
        _playerKnockbackVelocity = MathHelper.Lerp(_playerKnockbackVelocity, 0f, Math.Min(1f, elapsedSeconds * 8f));
        _verticalVelocity += Gravity * elapsedSeconds;
        _playerPosition.Y += _verticalVelocity * elapsedSeconds;
        _playerPosition.X = Math.Clamp(_playerPosition.X, 32f, _level.WorldBounds.Width - 32f);
        var landingTop = _level.FindLandingTop(
            _playerPosition.X,
            previousY: _playerPosition.Y - _verticalVelocity * elapsedSeconds,
            currentY: _playerPosition.Y,
            playerHalfWidth: PlayerHalfWidth);
        if (_verticalVelocity >= 0f && landingTop.HasValue)
        {
            _playerPosition.Y = landingTop.Value;
            _verticalVelocity = 0f;
            _isGrounded = true;
            _jumpsUsed = 0;
        }
        else
        {
            _isGrounded = false;
            _playerPosition.Y = Math.Max(32f, _playerPosition.Y);
        }
        CaptureCheckpoint();
        if (_runRemaining > 0f)
            _runRemaining = Math.Max(0f, _runRemaining - elapsedSeconds);
        _player.Update(elapsedSeconds);
        var footstep = _player.ConsumeFootstep();
        if (footstep && _isGrounded && Math.Abs(_moveInput.X) > 0.05f)
        {
            _audio.PlayFootstep(
                _level.GetSurfaceAt(_playerPosition.X, _playerPosition.Y),
                _player.Animation == PlayerAnimationState.Run);
        }
        ActivatePendingEnemySpawns();
        var visibleBounds = CameraWorldBounds;
        foreach (var enemy in _enemies)
        {
            enemy.Update(elapsedSeconds, _playerPosition, _level, visibleBounds);
            if (enemy.TryLaunchRangedAttack(
                    _playerPosition - new Vector2(0f, PlayerHeight / 2f),
                    out var origin,
                    out var direction))
                _enemyProjectiles.Add(new EnemyProjectile(enemy.Character, origin, direction));
        }
        if (_boss is not null)
        {
            _boss.Update(elapsedSeconds, _playerPosition);
            if (_boss.TryLaunchProjectiles(
                    _playerPosition - new Vector2(0f, PlayerHeight / 2f),
                    _enemyProjectiles,
                    out var castCue))
            {
                _audio.PlayCue(castCue, 0.75f);
                _effects.Spawn(_boss.Position - new Vector2(0f, 30f), CombatEffectKind.Magic, Vector2.UnitY, 0.9f);
            }
        }
        UpdateProjectiles(elapsedSeconds);
        UpdateEnemyProjectiles(elapsedSeconds);
        ResolveCombat();
        ResolvePlayerEnemyCollisions();
        _enemies.RemoveAll(enemy => enemy.IsDeathAnimationComplete);
        UpdateBossProgression();
        CollectLevelItems();
        if (!_player.IsActionActive)
        {
            var locomotion = !_isGrounded
                ? PlayerAnimationState.Jump
                : (_moveInput.Length() > 0.05f ? PlayerAnimationState.Walk : PlayerAnimationState.Idle);
            _player.SetLocomotion(locomotion);
        }
        if (PlayerAtLevelExit() && !string.IsNullOrWhiteSpace(_level.NextLevelId))
        {
            var nextLevelId = _level.NextLevelId;
            if (string.Equals(nextLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
            {
                _saveProgress.Act03Seal3RestorationApplied = false;
                _saveProgress.Act03FinalBossesCompleted = false;
            }
            LoadLevel(nextLevelId);
            _saveProgress.UnlockedLevelId = nextLevelId;
            StartAct01(nextLevelId);
            return;
        }
        if (_currentLevelId == "Act_01_C" && _finalBossDefeated && _finalBossRewardCollected)
        {
            LoadLevel("Act_02_A");
            _saveProgress.UnlockedLevelId = "Act_02_A";
            StartAct01("Act_02_A");
            return;
        }
        if (_currentLevelId == "Act_02_C" &&
            _finalBossDefeated &&
            _finalBossRewardCollected &&
            _saveProgress.SealAshesParts >= 2)
        {
            LoadLevel("Act_03_A");
            _saveProgress.UnlockedLevelId = "Act_03_A";
            StartAct01("Act_03_A");
            return;
        }
        if (_currentLevelId == "Act_03_C" &&
            _act03Encounter.Stage == Act03BossStage.Completed &&
            _saveProgress.SealAshesParts >= 4)
        {
            _saveProgress.CurrentLevelId = "Act_03_C";
            _saveProgress.UnlockedLevelId = "Act_03_C";
            _saveProgress.Act03FinalBossesCompleted = true;
            SaveCurrentProgress();
            EnterCreditsScreen();
            return;
        }
        UpdateCamera();
    }

    private void UpdateBossProgression()
    {
        if (!IsBossRewardLevel || _boss is null || !_boss.IsDeathAnimationComplete)
            return;

        if (!string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
        {
            if (_finalBossDefeated)
                return;

            _finalBossDefeated = true;
            RevealSealCollectibles(partIndex: null);
            _audio.PlayCue(AudioCue.Collectable, 1f);
            return;
        }

        var result = _act03Encounter.OnBossDefeated();
        if (!result.Transitioned)
            return;

        _boss = null;
        _enemyProjectiles.Clear();
        if (result.RevealedSealPartIndex.HasValue)
        {
            var partIndex = result.RevealedSealPartIndex.Value;
            if (partIndex == 3)
                _finalBossDefeated = true;

            RevealSealCollectibles(partIndex);
            _audio.PlayCue(AudioCue.Collectable, 1f);
            ShowBossBanner(LocalizedText.Get(partIndex == 3 ? "AshSeal3Revealed" : "AshSeal4Revealed"));
        }
        else if (!string.IsNullOrWhiteSpace(result.NextBossVariant))
        {
            SpawnFinalSequenceBoss(result.NextBossVariant);
        }
    }

    private void RevealSealCollectibles(int? partIndex)
    {
        foreach (var collectible in _level.Collectibles)
        {
            if (!IsSealCollectible(collectible))
                continue;

            if (partIndex.HasValue && GetSealPartIndex(collectible) != partIndex.Value)
                continue;

            _level.RevealCollectibleTile(collectible);
            _effects.Spawn(
                new Vector2(collectible.Bounds.Center.X, collectible.Bounds.Center.Y),
                CombatEffectKind.Magic,
                Vector2.UnitY,
                1.35f);
        }
    }

    private static bool IsSealCollectible(LevelObjectData collectible) =>
        collectible.Properties.TryGetValue("PickupKind", out var pickupKind) &&
        (string.Equals(pickupKind, nameof(CollectibleKind.SealRubyPart), StringComparison.OrdinalIgnoreCase) ||
         string.Equals(pickupKind, nameof(CollectibleKind.IronTalismanThird), StringComparison.OrdinalIgnoreCase) ||
         string.Equals(pickupKind, nameof(CollectibleKind.SealAshesPart), StringComparison.OrdinalIgnoreCase));

    private static int GetSealPartIndex(LevelObjectData collectible)
    {
        if (collectible.Properties.TryGetValue("SealPartIndex", out var configuredPart) &&
            int.TryParse(configuredPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPart))
            return Math.Clamp(parsedPart, 1, 4);
        return 1;
    }

    private bool IsSealCollectibleVisible(LevelObjectData collectible)
    {
        if (!string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
            return _finalBossDefeated && !_finalBossRewardCollected;

        return _act03Encounter.IsSealVisible(GetSealPartIndex(collectible), _saveProgress);
    }

    private bool IsSealCollectibleAvailable(LevelObjectData collectible)
    {
        if (!string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
            return _finalBossDefeated && !_finalBossRewardCollected;

        return _act03Encounter.IsSealAvailable(GetSealPartIndex(collectible), _saveProgress);
    }

    private bool PlayerAtLevelExit()
    {
        foreach (var exit in _level.LevelExits)
        {
            if (PlayerHitbox.Intersects(exit.Bounds))
                return true;
        }

        return false;
    }

    private void CollectLevelItems()
    {
        var maximumHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
        foreach (var collectible in _level.Collectibles)
        {
            if (_collectedCollectibles.Contains(collectible.Name) || !PlayerHitbox.Intersects(collectible.Bounds))
                continue;

            if (!collectible.Properties.TryGetValue("PickupKind", out var pickupKind) ||
                !Enum.TryParse<CollectibleKind>(pickupKind, true, out var kind))
                continue;

            if ((kind is CollectibleKind.SealRubyPart or CollectibleKind.IronTalismanThird or CollectibleKind.SealAshesPart) &&
                !IsSealCollectibleAvailable(collectible))
                continue;

            var collected = kind switch
            {
                CollectibleKind.Health => TryCollectHealth(collectible, maximumHealth),
                CollectibleKind.Stamina => TryCollectStamina(collectible, maximumHealth),
                CollectibleKind.PowerFruit => TryCollectPowerFruit(collectible),
                CollectibleKind.ExtraLife => TryCollectExtraLife(),
                CollectibleKind.SealRubyPart or CollectibleKind.IronTalismanThird or CollectibleKind.SealAshesPart => TryCollectSealRubyPart(collectible),
                _ => false
            };
            if (!collected)
                continue;

            _collectedCollectibles.Add(collectible.Name);
            _level.RemoveCollectibleTile(collectible);
            if (collectible.Properties.TryGetValue("PickupGroup", out var pickupGroup))
            {
                foreach (var groupedCollectible in _level.Collectibles)
                {
                    if (!groupedCollectible.Properties.TryGetValue("PickupGroup", out var group) ||
                        !string.Equals(group, pickupGroup, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _collectedCollectibles.Add(groupedCollectible.Name);
                    _level.RemoveCollectibleTile(groupedCollectible);
                }
            }
            _audio.PlayCue(AudioCue.Collectable, 0.9f);
            _effects.Spawn(new Vector2(collectible.Bounds.Center.X, collectible.Bounds.Top), CombatEffectKind.Magic, Vector2.UnitY, 0.8f);
            SaveCurrentProgress();
        }

    }

    private bool TryCollectHealth(LevelObjectData collectible, int maximumHealth)
    {
        if (_playerHealth >= maximumHealth)
            return TryCollectExtraLife();

        var restore = Math.Max(1, maximumHealth / 4);
        if (collectible.Properties.TryGetValue("RestoreHealth", out var configuredRestore) &&
            int.TryParse(configuredRestore, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRestore))
            restore = Math.Max(1, parsedRestore);

        _playerHealth = Math.Min(maximumHealth, _playerHealth + restore);
        return true;
    }

    private bool TryCollectStamina(LevelObjectData collectible, int maximumHealth)
    {
        if (_stamina >= StaminaMaximum)
        {
            var restoreHealth = Math.Max(1, (int)Math.Ceiling(maximumHealth * 0.125f));
            _playerHealth = Math.Min(maximumHealth, _playerHealth + restoreHealth);
            return true;
        }

        var restore = 1.5f;
        if (collectible.Properties.TryGetValue("RestoreStamina", out var configuredRestore) &&
            float.TryParse(configuredRestore, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRestore))
            restore = Math.Max(0.1f, parsedRestore);

        _stamina = Math.Min(StaminaMaximum, _stamina + restore);
        return true;
    }

    private bool TryCollectExtraLife()
    {
        if (_livesRemaining >= MaximumLives)
            return false;

        _livesRemaining++;
        return true;
    }

    private bool TryCollectPowerFruit(LevelObjectData collectible)
    {
        if ((IsKunoichi && _power < CombatBalance.SpecialPowerCost) ||
            (!IsKunoichi && _power >= CombatBalance.PlayerPowerMaximum))
            return false;

        var restore = CombatBalance.PowerFruitRestore;
        if (collectible.Properties.TryGetValue("RestorePower", out var configuredRestore) &&
            float.TryParse(configuredRestore, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRestore))
            restore = Math.Max(0.1f, parsedRestore);

        _power = Math.Min(CombatBalance.PlayerPowerMaximum, _power + restore);
        if (IsKunoichi)
        {
            ConsumeKunoichiFruit();
            if (CanStartAction())
                StartAction(PlayerAnimationState.Eating);
        }
        return true;
    }

    private void ConsumeKunoichiFruit()
    {
        if (_stamina < StaminaMaximum)
        {
            _stamina = Math.Min(
                StaminaMaximum,
                _stamina + CombatBalance.KunoichiFruitStaminaRestore);
            return;
        }

        var maximumHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
        if (_playerHealth < maximumHealth)
        {
            var restore = Math.Max(
                1,
                (int)Math.Ceiling(maximumHealth * CombatBalance.KunoichiFruitHealthRestoreFraction));
            _playerHealth = Math.Min(maximumHealth, _playerHealth + restore);
            return;
        }

        TryCollectExtraLife();
    }

    private bool TryCollectSealRubyPart(LevelObjectData collectible)
    {
        var partIndex = GetSealPartIndex(collectible);
        if (string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase))
        {
            var result = _act03Encounter.TryCollectSeal(partIndex, _saveProgress);
            if (!result.Collected)
                return false;

            if (result.ShouldRestorePlayerResources)
            {
                Act03EncounterStateMachine.ApplySeal3ResourceRestoration(
                    _selectedDifficulty,
                    StaminaMaximum,
                    ref _livesRemaining,
                    ref _playerHealth,
                    ref _stamina,
                    ref _power,
                    ref _powerRecoveryElapsed);
            }

            if (result.ShouldOpenCredits)
                _finalBossRewardCollected = true;

            if (!string.IsNullOrWhiteSpace(result.NextBossVariant))
                SpawnFinalSequenceBoss(result.NextBossVariant);

            return true;
        }

        if (_finalBossRewardCollected)
            return false;

        _finalBossRewardCollected = true;
        _saveProgress.SealAshesParts = Math.Clamp(Math.Max(_saveProgress.SealAshesParts, partIndex), 0, 4);
        return true;
    }

    private bool IsBossRewardLevel =>
        string.Equals(_currentLevelId, "Act_01_C", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_currentLevelId, "Act_02_C", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_currentLevelId, "Act_03_C", StringComparison.OrdinalIgnoreCase);

    private Rectangle PlayerHitbox => new(
        (int)(_playerPosition.X - PlayerHalfWidth),
        (int)(_playerPosition.Y - PlayerHeight),
        (int)(PlayerHalfWidth * 2f),
        (int)PlayerHeight);

    private Rectangle PlayerAttackHitbox => new(
        (int)(_playerPosition.X + (_playerFacing > 0 ? 4f : -38f)),
        (int)(_playerPosition.Y - 28f),
        34,
        22);

    private bool IsKunoichi => string.Equals(_player.Id, "Kunoichi", StringComparison.Ordinal);
    private bool IsMonk => string.Equals(_player.Id, "Ninja_Monk", StringComparison.Ordinal);

    private void StartPrimarySpecial()
    {
        if (!CanStartAction())
            return;

        if (IsKunoichi)
        {
            if (_power < CombatBalance.SpecialPowerCost)
                return;

            StartAction(PlayerAnimationState.Eating);
            return;
        }

        if (IsMonk)
        {
            if (_power < CombatBalance.SpecialPowerCost)
                return;

#if DEBUG
            if (!_debugCheatCodes.InfinitePower)
                _power -= CombatBalance.SpecialPowerCost;
#else
            _power -= CombatBalance.SpecialPowerCost;
#endif
            StartAction(PlayerAnimationState.Blade);
            _playerAttackHitApplied = false;
        }
        else
        {
            StartAction(PlayerAnimationState.Shot);
            SpawnSpecialProjectile(PlayerProjectileKind.Dart);
        }
    }

    private void StartSecondarySpecial()
    {
        if (!CanStartAction())
            return;

        if (IsKunoichi)
        {
            if (!TrySpendAvailablePower())
                return;

            StartAction(PlayerAnimationState.Cast);
            SpawnSpecialProjectile(PlayerProjectileKind.Spine);
            return;
        }

        if (IsMonk || !TrySpendAvailablePower())
            return;

        StartAction(PlayerAnimationState.Disguise);
    }

    private bool TrySpendAvailablePower()
    {
        if (_power <= 0f)
            return false;

#if DEBUG
        if (_debugCheatCodes.InfinitePower)
            return true;
#endif
        _power = Math.Max(0f, _power - CombatBalance.GetSecondarySpecialPowerCost(_selectedDifficulty));
        return true;
    }

    private void UpdatePlayerPowerRecovery(float elapsedSeconds)
    {
        if (_power >= CombatBalance.PlayerPowerMaximum)
        {
            _powerRecoveryElapsed = 0f;
            return;
        }

        _powerRecoveryElapsed += elapsedSeconds;
        while (_powerRecoveryElapsed >= CombatBalance.PlayerPowerRecoveryIntervalSeconds &&
               _power < CombatBalance.PlayerPowerMaximum)
        {
            _powerRecoveryElapsed -= CombatBalance.PlayerPowerRecoveryIntervalSeconds;
            _power = Math.Min(
                CombatBalance.PlayerPowerMaximum,
                _power + CombatBalance.GetPlayerPowerRecoveryAmount(_selectedDifficulty));
        }

        if (_power >= CombatBalance.PlayerPowerMaximum)
            _powerRecoveryElapsed = 0f;
    }

    private bool CanStartAction() => !_player.IsActionActive && _player.Animation != PlayerAnimationState.Dead;

    private void SpawnSpecialProjectile(PlayerProjectileKind kind)
    {
        var texture = kind switch
        {
            PlayerProjectileKind.Spine => _spineTexture,
            PlayerProjectileKind.Kunai => _kunaiTexture,
            _ => _dartTexture
        };
        var source = kind == PlayerProjectileKind.Kunai
            ? new Rectangle(0, 0, Math.Min(16, texture.Width), Math.Min(16, texture.Height))
            : new Rectangle(0, 0, texture.Width, texture.Height);
        var position = _playerPosition + new Vector2(_playerFacing * 20f, -20f);
        _projectiles.Add(new PlayerProjectile(texture, source, position, _playerFacing, kind));
    }

    private void UpdateProjectiles(float elapsedSeconds)
    {
        var worldBounds = new Rectangle(0, 0, (int)_level.WorldBounds.Width, (int)_level.WorldBounds.Height);
        for (var index = _projectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _projectiles[index];
            projectile.Update(elapsedSeconds, worldBounds);
            if (projectile.IsExpired)
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            var hit = false;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive || !projectile.Hitbox.Intersects(enemy.Hitbox))
                    continue;

                if (enemy.TakeDamage(CombatBalance.SpecialAttackDamage, projectile.Position.X))
                {
                    hit = true;
                    OnSpecialProjectileHit(projectile, !enemy.IsAlive, enemy.Position - new Vector2(0f, 18f));
                }

                break;
            }

            if (!hit && _boss is not null && _boss.IsAlive && projectile.Hitbox.Intersects(_boss.Hitbox))
            {
                hit = true;
                var wasAlive = _boss.IsAlive;
                _boss.TakeDamage(CombatBalance.SpecialAttackDamage, projectile.Position.Y);
                OnSpecialProjectileHit(projectile, wasAlive && !_boss.IsAlive, _boss.Position - new Vector2(0f, 26f));
            }

            if (hit)
                _projectiles.RemoveAt(index);
        }
    }

    private void OnSpecialProjectileHit(PlayerProjectile projectile, bool killed, Vector2 effectPosition)
    {
        _audio.PlayCue(AudioCue.SlashImpact, 0.85f);
        _effects.Spawn(effectPosition, CombatEffectKind.Impact, new Vector2(_playerFacing, -0.2f), 1.05f);
        if (!killed)
            return;

        _effects.Spawn(effectPosition, CombatEffectKind.Death, Vector2.UnitY, 1.1f);
        _audio.PlayCue(AudioCue.Explosion, 0.55f);
    }

    private void UpdateEnemyProjectiles(float elapsedSeconds)
    {
        var worldBounds = new Rectangle(0, 0, (int)_level.WorldBounds.Width, (int)_level.WorldBounds.Height);
        var playerHitbox = PlayerHitbox;
        for (var index = _enemyProjectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _enemyProjectiles[index];
            projectile.Update(elapsedSeconds, worldBounds);
            if (projectile.IsExpired)
            {
                _enemyProjectiles.RemoveAt(index);
                continue;
            }

            if (!projectile.Hitbox.Intersects(playerHitbox))
                continue;

            if (TakePlayerDamage(projectile.Damage, projectile.SourceX))
            {
                _audio.PlayCue(projectile.ImpactCue, 0.75f);
                _effects.Spawn(
                    _playerPosition - new Vector2(0f, 18f),
                    projectile.ImpactEffect,
                    projectile.Direction);
            }

            _enemyProjectiles.RemoveAt(index);
        }
    }

    private void ResolveCombat()
    {
        if (!_playerAttackHitApplied && _player.IsActionActive &&
            _player.Animation is PlayerAnimationState.Attack1 or PlayerAnimationState.Attack2 or PlayerAnimationState.Blade)
        {
            foreach (var enemy in _enemies)
            {
                if (!_playerAttackHitApplied && enemy.IsAlive && PlayerAttackHitbox.Intersects(enemy.Hitbox) &&
                    enemy.TakeDamage(
                        GetPlayerAttackDamage(enemy.MaxHealth),
                        _playerPosition.X))
                {
                    _playerAttackHitApplied = true;
                    _audio.PlayCue(AudioCue.SlashImpact, 0.9f);
                    _effects.Spawn(enemy.Position - new Vector2(0f, 18f), CombatEffectKind.Impact, new Vector2(_playerFacing, -0.25f));
                    if (!enemy.IsAlive)
                    {
                        _audio.PlayCue(AudioCue.Explosion, 0.55f);
                        _effects.Spawn(enemy.Position - new Vector2(0f, 18f), CombatEffectKind.Death, Vector2.UnitY, 1.1f);
                    }

                    break;
                }
            }

            if (!_playerAttackHitApplied && _boss is not null && _boss.IsAlive && PlayerAttackHitbox.Intersects(_boss.Hitbox) &&
                _boss.TakeDamage(
                    GetPlayerAttackDamage(_boss.MaxHealth),
                    PlayerAttackHitbox.Center.Y))
            {
                _playerAttackHitApplied = true;
                _audio.PlayCue(AudioCue.SlashImpact, 1f);
                _effects.Spawn(_boss.Position - new Vector2(0f, 26f), CombatEffectKind.Impact, new Vector2(_playerFacing, -0.25f), 1.3f);
                if (!_boss.IsAlive)
                {
                    _audio.PlayCue(AudioCue.Explosion, 0.8f);
                    _effects.Spawn(_boss.Position - new Vector2(0f, 26f), CombatEffectKind.Death, Vector2.UnitY, 1.6f);
                }
            }
        }

        var playerHitbox = PlayerHitbox;
        foreach (var enemy in _enemies)
        {
            if (enemy.TryConsumeAttackHit(playerHitbox))
            {
                if (TakePlayerDamage(CombatBalance.EnemyAttackDamage, enemy.Position.X))
                {
                    _audio.PlayCue(AudioCue.SlashImpact, 0.75f);
                    _effects.Spawn(
                        _playerPosition - new Vector2(0f, 18f),
                        CombatEffectKind.Impact,
                        new Vector2(enemy.Position.X - _playerPosition.X, -0.1f));
                }
            }
        }

        if (_boss is not null && _boss.TryConsumeAttackHit(playerHitbox))
        {
            if (TakePlayerDamage(CombatBalance.EnemyAttackDamage * 2, _boss.Position.X))
            {
                _audio.PlayCue(AudioCue.FlameImpact, 0.8f);
                _effects.Spawn(_playerPosition - new Vector2(0f, 20f), CombatEffectKind.Impact, new Vector2(_boss.Position.X - _playerPosition.X, -0.1f), 1.3f);
            }
        }
    }

    private int GetPlayerAttackDamage(int targetMaxHealth)
    {
        var damage = _player.Animation == PlayerAnimationState.Blade
            ? CombatBalance.MonkBladeDamage
            : CombatBalance.PlayerAttackDamage;
#if DEBUG
        if (_debugCheatCodes.OneHitDamage)
        {
            // Boss damage is scaled by hit height, so use a value that still
            // guarantees 100% damage at the minimum 0.75x multiplier.
            damage = (int)Math.Ceiling(targetMaxHealth / 0.75f);
        }
#endif
        return damage;
    }

    private void ResolvePlayerEnemyCollisions()
    {
        var playerHitbox = PlayerHitbox;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive || !playerHitbox.Intersects(enemy.Hitbox))
                continue;

            var playerCenter = playerHitbox.Center.X;
            var enemyCenter = enemy.Hitbox.Center.X;
            var direction = playerCenter < enemyCenter ? -1f : 1f;
            var overlap = playerCenter < enemyCenter
                ? playerHitbox.Right - enemy.Hitbox.Left
                : enemy.Hitbox.Right - playerHitbox.Left;
            var separation = Math.Max(1f, overlap + 1f);
            _playerPosition.X += direction * separation;
            enemy.PushHorizontally(-direction * separation * 0.5f);
            _playerPosition.X = Math.Clamp(_playerPosition.X, 32f, _level.WorldBounds.Width - 32f);
        }

        if (_boss is not null && _boss.IsAlive && playerHitbox.Intersects(_boss.Hitbox))
        {
            var direction = playerHitbox.Center.X < _boss.Hitbox.Center.X ? -1f : 1f;
            _playerPosition.X += direction * 4f;
        }
    }

    private bool TakePlayerDamage(int damage, float sourceX)
    {
#if DEBUG
        if (_debugCheatCodes.InfiniteHealth)
            return false;
#endif

        if (_playerInvulnerability > 0f || _player.Animation == PlayerAnimationState.Disguise || _playerHealth <= 0)
            return false;

        _playerHealth = Math.Max(0, _playerHealth - damage);
        _playerInvulnerability = 0.75f;
        _playerKnockbackVelocity = _playerPosition.X < sourceX ? -96f : 96f;
        _verticalVelocity = -110f;
        _isGrounded = false;

        if (_playerHealth == 0)
            _player.SetDead();
        else
            _player.SetHurt();

        return true;
    }

    private void ResetPlayerAfterDefeat()
    {
        if (_livesRemaining <= 0)
        {
            SaveCurrentProgress();
            _screen = GameScreen.GameOver;
            _audio.PlayGameOverMusic();
            return;
        }

        _livesRemaining--;
        _playerHealth = CombatBalance.GetPlayerMaxHealth(_selectedDifficulty);
        _playerPosition = _checkpointPosition;
        _verticalVelocity = 0f;
        _playerKnockbackVelocity = 0f;
        _isGrounded = true;
        _jumpsUsed = 0;
        _playerInvulnerability = 1f;
        _player.Reset();
        _enemyProjectiles.Clear();
        _boss?.Reset();
        SaveCurrentProgress();
    }

    private void StartAction(PlayerAnimationState action)
    {
        if (action != PlayerAnimationState.Jump && !CanStartAction())
            return;

        if (action == PlayerAnimationState.Run && _stamina < RunStaminaCost)
            return;

        _player.SetAction(action);
        if (action is PlayerAnimationState.Attack1 or PlayerAnimationState.Attack2 or PlayerAnimationState.Blade)
            _playerAttackHitApplied = false;
        _runRemaining = action == PlayerAnimationState.Run ? _player.ActionRemaining : 0f;
        if (action == PlayerAnimationState.Run)
        {
#if DEBUG
            if (!_debugCheatCodes.InfiniteStamina)
                _stamina = Math.Max(0f, _stamina - RunStaminaCost);
#else
            _stamina = Math.Max(0f, _stamina - RunStaminaCost);
#endif
            _effects.Spawn(_playerPosition - new Vector2(0f, 2f), CombatEffectKind.Dust, new Vector2(-_playerFacing, 0f), 0.75f);
        }
        else if (action == PlayerAnimationState.Attack1)
        {
            _audio.PlayCue(AudioCue.Sword, 0.8f);
            _effects.Spawn(_playerPosition - new Vector2(-_playerFacing * 10f, 18f), CombatEffectKind.Slash, new Vector2(_playerFacing, -0.2f));
        }
        else if (action == PlayerAnimationState.Attack2)
        {
            _audio.PlayCue(AudioCue.Whoosh, 0.65f);
            _effects.Spawn(_playerPosition - new Vector2(-_playerFacing * 10f, 18f), CombatEffectKind.Slash, new Vector2(_playerFacing, -0.2f));
        }
        else if (action == PlayerAnimationState.Cast)
        {
            _audio.PlayCue(AudioCue.Magic, 0.75f);
            _effects.Spawn(_playerPosition - new Vector2(-_playerFacing * 10f, 20f), CombatEffectKind.Magic, new Vector2(_playerFacing, 0f), 1.1f);
        }
        else if (action is PlayerAnimationState.Blade or PlayerAnimationState.Shot)
        {
            _audio.PlayCue(AudioCue.Whoosh, 0.75f);
            _effects.Spawn(_playerPosition - new Vector2(-_playerFacing * 10f, 18f), CombatEffectKind.Slash, new Vector2(_playerFacing, -0.1f), 0.9f);
        }
        else if (action == PlayerAnimationState.Eating)
        {
            _audio.PlayCue(AudioCue.Collectable, 0.7f);
        }
        else if (action == PlayerAnimationState.Disguise)
        {
            _effects.Spawn(_playerPosition - new Vector2(0f, 18f), CombatEffectKind.Magic, Vector2.UnitY, 0.9f);
        }
    }

    private void TryJump()
    {
        if (_jumpsUsed >= MaximumJumps)
            return;

        _verticalVelocity = _jumpsUsed == 0
            ? JumpVelocity
            : (_runRemaining > 0f ? RunningDoubleJumpVelocity : DoubleJumpVelocity);
        _isGrounded = false;
        _jumpsUsed++;
        StartAction(PlayerAnimationState.Jump);
    }

    private void UpdateCamera()
    {
        _camera.LookAt(CameraCenter);
    }

    private Vector2 CameraCenter
    {
        get
        {
            var halfWidth = VirtualWidth / 2f;
            var halfHeight = VirtualHeight / 2f;
            return new Vector2(
                Math.Clamp(_playerPosition.X, halfWidth, _level.WorldBounds.Width - halfWidth),
                Math.Clamp(_playerPosition.Y, halfHeight, _level.WorldBounds.Height - halfHeight));
        }
    }

    private Rectangle CameraWorldBounds => new(
        (int)(CameraCenter.X - VirtualWidth / 2f),
        (int)(CameraCenter.Y - VirtualHeight / 2f),
        VirtualWidth,
        VirtualHeight);

    private void ToggleFullscreen()
    {
        if (!IsDesktop)
            return;

        _fullScreen = !_fullScreen;
        _graphics.IsFullScreen = _fullScreen;
        _graphics.ApplyChanges();
        SaveCurrentSettings();
    }

    private void SaveCurrentSettings()
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(_visualValidationDir))
            return;
#endif
        _settings.BgmVolume = _audio?.BgmVolume ?? _settings.BgmVolume;
        _settings.SfxVolume = _audio?.SfxVolume ?? _settings.SfxVolume;
        _settings.IsMuted = _audio?.IsMuted ?? _settings.IsMuted;
        _settings.Language = _languageSelector.SelectedCode;
        _settings.FullScreen = _fullScreen;
        GameSettingsStore.Save(_settings);
    }

    private void SaveCurrentProgress()
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(_visualValidationDir))
            return;
#endif
        _saveProgress.CurrentLevelId = _currentLevelId;
        _saveProgress.CharacterId = _characterIds[_characterSelection];
        _saveProgress.Difficulty = _selectedDifficulty;
        _saveProgress.LivesRemaining = _livesRemaining;
        _saveProgress.CheckpointLevelId = _currentLevelId;
        _saveProgress.CheckpointId = _checkpointId;
        SaveProgressStore.Save(_saveProgress);
    }

    private void DrawMainMenu()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();

        DrawMenuBackground();
        DrawRect(new Rectangle(12, 12, VirtualWidth - 24, VirtualHeight - 24), new Color(25, 35, 45), false, 2);
        var title = LocalizedText.Get("Title").ToUpperInvariant();
        var titleScale = Math.Min(1.25f, (VirtualWidth - 32f) / Math.Max(1f, _fontSemiboldWide.MeasureString(title).X));
        DrawTextCentered(title, new Vector2(VirtualWidth * 0.5f, 30f), new Color(214, 191, 135), _fontSemiboldWide, titleScale);
        for (var index = 0; index < _menuKeys.Length; index++)
        {
            var selected = index == _menuSelection;
            var bounds = MenuButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get(_menuKeys[index]), bounds.Center.ToVector2(), selected ? Color.White : new Color(188, 193, 188), 0.85f);
        }

        EndUi();
    }

    private const float IntroSafeHorizontalMargin = 16f;
    private const float IntroMaxHorizontalScale = 1.03f;

    private List<IntroParagraphLine> GetIntroLayoutLines()
    {
        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (_cachedIntroLines.Count > 0 &&
            string.Equals(_cachedIntroCulture, cultureName, StringComparison.OrdinalIgnoreCase))
            return _cachedIntroLines;

        var rawLines = new string[IntroLineKeys.Length];
        for (var i = 0; i < IntroLineKeys.Length; i++)
            rawLines[i] = LocalizedText.Get(IntroLineKeys[i]);

        var safeWidth = VirtualWidth - IntroSafeHorizontalMargin * 2f;
        var unscaledMaxWidth = safeWidth / IntroMaxHorizontalScale;
        _cachedIntroLines = TextLayoutEngine.LayoutIntroParagraphs(
            rawLines,
            unscaledMaxWidth,
            text => _fontWide.MeasureString(text).X);
        _cachedIntroCulture = cultureName;
        return _cachedIntroLines;
    }

    private void DrawIntro()
    {
        GraphicsDevice.Clear(new Color(3, 5, 9));
        BeginUi();

        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(3, 5, 9));

        var introLines = GetIntroLayoutLines();
        var startY = VirtualHeight - 20f - _introScroll;
        var originY = _fontWide.LineSpacing / 2f;

        for (var index = 0; index < introLines.Count; index++)
        {
            var line = introLines[index];
            if (line.IsEmpty)
                continue;

            var y = startY + index * IntroLineHeight;
            if (y < -18f || y > VirtualHeight + 18f)
                continue;

            var depth = Math.Clamp((y + 18f) / (VirtualHeight + 36f), 0f, 1f);
            var horizontalScale = 0.48f + depth * 0.55f;
            var verticalScale = 0.62f;
            var color = Color.Lerp(new Color(194, 158, 74), new Color(255, 224, 143), depth);
            var scale = new Vector2(horizontalScale, verticalScale);

            foreach (var word in line.Words)
            {
                var drawX = VirtualWidth / 2f + (word.X - line.BlockWidth / 2f) * horizontalScale;
                _spriteBatch.DrawString(
                    _fontWide,
                    word.Text,
                    new Vector2(drawX, y),
                    color,
                    0f,
                    new Vector2(0f, originY),
                    scale,
                    SpriteEffects.None,
                    0f);
            }
        }

        var fadeHeight = 22;
        DrawRect(new Rectangle(0, 0, VirtualWidth, fadeHeight), new Color(3, 5, 9, 235));
        DrawRect(new Rectangle(0, VirtualHeight - fadeHeight, VirtualWidth, fadeHeight), new Color(3, 5, 9, 235));
        EndUi();
    }

    private void DrawGameOver()
    {
        GraphicsDevice.Clear(new Color(12, 5, 9));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(25, 9, 15));
        DrawTextCentered(LocalizedText.Get("GameOver"), new Vector2(VirtualWidth / 2f, 70f), new Color(214, 86, 76), _fontSemiboldWide, 1.25f);
        DrawTextCentered(LocalizedText.Get("PressToContinue"), new Vector2(VirtualWidth / 2f, 112f), new Color(214, 191, 135), 0.58f);
        EndUi();
    }

    private void DrawVictory()
    {
        GraphicsDevice.Clear(new Color(6, 16, 18));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(12, 31, 34));
        DrawTextCentered(LocalizedText.Get("Victory"), new Vector2(VirtualWidth / 2f, 70f), new Color(214, 191, 135), _fontSemiboldWide, 1.25f);
        DrawTextCentered(LocalizedText.Get("PressToContinue"), new Vector2(VirtualWidth / 2f, 112f), new Color(156, 201, 191), 0.58f);
        EndUi();
    }

    private void EnterCreditsScreen()
    {
        _creditsScroll = 0f;
        _bossBannerRemaining = 0f;
        _screen = GameScreen.Credits;
        _audio.PlayCreditsMusic();
    }

    private void UpdateCredits(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        _creditsScroll += (float)gameTime.ElapsedGameTime.TotalSeconds * CreditsScrollSpeed;
        var totalHeight = VirtualHeight + (CreditsLineKeys.Length + 4) * CreditsLineHeight + 36f;
        var creditsFinished = _creditsScroll >= totalHeight;
        var skipRequested = IsNewKeyPress(keyboard, Keys.Enter) ||
                            IsNewKeyPress(keyboard, Keys.Space) ||
                            IsNewButtonPress(gamePad, Buttons.A) ||
                            IsNewButtonPress(gamePad, Buttons.B) ||
                            IsNewButtonPress(gamePad, Buttons.Start) ||
                            GetPressedPoint(touches).HasValue;

        if (creditsFinished || skipRequested)
        {
            _creditsScroll = 0f;
            _screen = GameScreen.MainMenu;
            _audio.PlayMenuMusic();
        }
    }

    private void DrawCredits()
    {
        GraphicsDevice.Clear(new Color(5, 7, 13));
        BeginUi();

        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(5, 7, 13));
        DrawRect(new Rectangle(10, 10, VirtualWidth - 20, VirtualHeight - 20), new Color(46, 58, 72), false, 1);

        var startY = VirtualHeight - 16f - _creditsScroll;
        var title = LocalizedText.Get("Title").ToUpperInvariant();
        if (startY >= -24f && startY <= VirtualHeight + 24f)
        {
            var titleScale = Math.Min(1.05f, (VirtualWidth - 40f) / Math.Max(1f, _fontSemiboldWide.MeasureString(title).X));
            DrawTextCentered(title, new Vector2(VirtualWidth / 2f, startY), new Color(214, 191, 135), _fontSemiboldWide, new Vector2(titleScale, titleScale));
        }

        for (var index = 0; index < CreditsLineKeys.Length; index++)
        {
            var key = CreditsLineKeys[index];
            var line = key == "CreditsSpacer" ? string.Empty : LocalizedText.Get(key);
            if (line.Length == 0)
                continue;

            var y = startY + 24f + index * CreditsLineHeight;
            if (y < -14f || y > VirtualHeight + 14f)
                continue;

            var isHeading = key.StartsWith("CreditsHeading", StringComparison.Ordinal) ||
                            key.StartsWith("CreditsSection", StringComparison.Ordinal) ||
                            key == "CreditsThankYou";
            var color = isHeading ? new Color(214, 191, 135) : new Color(204, 214, 216);
            var scale = key == "CreditsHeadingComplete" ? 0.68f : (isHeading ? 0.54f : 0.48f);
            DrawTextCentered(line, new Vector2(VirtualWidth / 2f, y), color, isHeading ? _fontSemibold : _font, new Vector2(scale, scale));
        }

        DrawRect(new Rectangle(0, 0, VirtualWidth, 18), new Color(5, 7, 13, 235));
        DrawRect(new Rectangle(0, VirtualHeight - 20, VirtualWidth, 20), new Color(5, 7, 13, 235));
        DrawTextCentered(LocalizedText.Get("CreditsSkipHint"), new Vector2(VirtualWidth / 2f, VirtualHeight - 10f), new Color(156, 170, 169), 0.42f);
        EndUi();
    }

    private void DrawOptions()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawMenuBackground();
        DrawTextCentered(LocalizedText.Get("Options"), new Vector2(VirtualWidth / 2f, 22f), new Color(214, 191, 135), _fontSemibold, 1.15f);
        DrawTextCentered(LocalizedText.Get("Language"), new Vector2(VirtualWidth / 2f, 38f), new Color(156, 170, 169), 0.52f);

        var languageSelected = _optionsSelection == LanguageOptionIndex;
        var selectorBounds = LanguageSelectorBounds();
        var prevArrow = LanguagePreviousArrowButton();
        var nextArrow = LanguageNextArrowButton();
        var fillColor = languageSelected ? new Color(78, 96, 105) : new Color(42, 53, 63);
        var borderColor = languageSelected ? new Color(214, 191, 135) : new Color(77, 91, 99);
        var arrowFill = languageSelected ? new Color(58, 74, 84) : new Color(34, 44, 53);
        var arrowColor = languageSelected ? new Color(246, 214, 148) : new Color(156, 170, 169);

        DrawRect(selectorBounds, fillColor);
        DrawRect(prevArrow, arrowFill);
        DrawRect(nextArrow, arrowFill);
        DrawRect(selectorBounds, borderColor, false, languageSelected ? 2 : 1);
        DrawRect(prevArrow, borderColor, false, 1);
        DrawRect(nextArrow, borderColor, false, 1);

        DrawTextCentered("<", prevArrow.Center.ToVector2(), arrowColor, _fontSemibold, 0.72f);
        DrawTextCentered(">", nextArrow.Center.ToVector2(), arrowColor, _fontSemibold, 0.72f);

        var currentCulture = _languageSelector.SelectedCulture;
        var languageLabel = $"{currentCulture.Name} - {LocalizationManager.GetDisplayLabel(currentCulture)}";
        DrawTextCentered(
            languageLabel,
            selectorBounds.Center.ToVector2(),
            languageSelected ? new Color(255, 244, 214) : Color.White,
            _fontSemibold,
            0.58f);

        var cultureCount = _languageSelector.SupportedCultures.Count;
        if (cultureCount > 1)
        {
            const int dotSize = 3;
            const int dotGap = 6;
            var totalDotsWidth = cultureCount * dotSize + (cultureCount - 1) * dotGap;
            var dotStartX = (VirtualWidth - totalDotsWidth) / 2;
            var dotY = selectorBounds.Bottom + 4;
            for (var index = 0; index < cultureCount; index++)
            {
                var isActive = index == _languageSelector.SelectedIndex;
                var dotColor = isActive
                    ? new Color(214, 191, 135)
                    : new Color(92, 106, 114);
                DrawRect(new Rectangle(dotStartX + index * (dotSize + dotGap), dotY, dotSize, dotSize), dotColor);
            }
        }

        if (IsDesktop)
        {
            var controls = ControlsButton();
            var selected = _optionsSelection == ControlsOptionIndex;
            DrawRect(controls, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(controls, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get("Controls"), controls.Center.ToVector2(), Color.White, 0.56f);

            DrawAudioOption(FullScreenButton(), _optionsSelection == FullScreenOptionIndex,
                $"{LocalizedText.Get("Fullscreen")}: {(_fullScreen ? LocalizedText.Get("On") : LocalizedText.Get("Off"))}");
        }

        DrawAudioOption(BgmVolumeButton(), _optionsSelection == BgmVolumeOptionIndex,
            $"{LocalizedText.Get("BgmVolume")}: {VolumePercent(_audio.BgmVolume)}");
        DrawAudioOption(SfxVolumeButton(), _optionsSelection == SfxVolumeOptionIndex,
            $"{LocalizedText.Get("SfxVolume")}: {VolumePercent(_audio.SfxVolume)}");
        DrawAudioOption(MuteButton(), _optionsSelection == MuteOptionIndex,
            $"{LocalizedText.Get("Mute")}: {(_audio.IsMuted ? LocalizedText.Get("On") : LocalizedText.Get("Off"))}");

        if (IsMobile)
        {
            var back = OptionsBackButton();
            var backSelected = _optionsSelection == BackOptionIndex;
            DrawRect(back, backSelected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(back, backSelected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.62f);
        }
        EndUi();
    }

    private void DrawAudioOption(Rectangle bounds, bool selected, string label)
    {
        DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(label, bounds.Center.ToVector2(), Color.White, 0.52f);
    }

    private void DrawMenuBackground()
    {
        _spriteBatch.Draw(_mainBackground, new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(7, 14, 23, 112));
    }

    private void DrawGameplayBackground()
    {
        _spriteBatch.Draw(_gameplayBackground, new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(7, 14, 23, 96));
    }

    private string VolumePercent(float volume) => $"{(int)Math.Round(volume * 100f)}%";

    private void DrawControls()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawMenuBackground();
        DrawTextCentered(LocalizedText.Get("Controls"), new Vector2(VirtualWidth / 2f, 13f), new Color(214, 191, 135), _fontSemiboldWide, 0.95f);

        var keyboardButton = ControlsKeyboardButton();
        var gamepadButton = ControlsGamepadButton();
        DrawRect(keyboardButton, _bindingDevice == BindingDevice.Keyboard ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(keyboardButton, _bindingDevice == BindingDevice.Keyboard ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Keyboard"), keyboardButton.Center.ToVector2(), Color.White, 0.58f);
        DrawRect(gamepadButton, _bindingDevice == BindingDevice.Gamepad ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(gamepadButton, _bindingDevice == BindingDevice.Gamepad ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Gamepad"), gamepadButton.Center.ToVector2(), Color.White, 0.58f);

        for (var index = 0; index < _controlKeys.Length; index++)
        {
            var row = ControlRow(index);
            var selected = index == _controlSelection;
            DrawRect(row, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(row, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            var binding = _bindingDevice == BindingDevice.Keyboard
                ? GetKeyboardBindingLabel(index)
                : GetGamepadBindingLabel(index);
            DrawTextCentered($"{LocalizedText.Get(_controlKeys[index])}: {binding}", row.Center.ToVector2(), Color.White, 0.5f);
        }

        var instruction = _waitingForBinding
            ? LocalizedText.Get("PressKey")
            : $"{LocalizedText.Get("Keyboard")}/{LocalizedText.Get("Gamepad")}  ENTER = {LocalizedText.Get("Bind")}";
        DrawTextCentered(instruction, new Vector2(VirtualWidth / 2f, 158f), new Color(156, 170, 169), 0.4f);

        if (IsMobile)
        {
            var back = ControlsBackButton();
            DrawRect(back, new Color(42, 53, 63));
            DrawRect(back, new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.62f);
        }
        EndUi();
    }

    private void DrawPlayerSelection()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawGameplayBackground();
        DrawTextCentered(LocalizedText.Get("SelectPlayer"), new Vector2(VirtualWidth / 2f, 20f), new Color(214, 191, 135), 1f);
        DrawTextCentered("<  >", new Vector2(VirtualWidth / 2f, 38f), new Color(130, 145, 149), 0.72f);

        for (var index = 0; index < _characterIds.Length; index++)
        {
            var selected = index == _characterSelection;
            var bounds = CharacterSelectionButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(CharacterLabel(index), bounds.Center.ToVector2(), Color.White, 0.52f);
        }

        _player.SetLocomotion(PlayerAnimationState.Idle);
        _player.Draw(_spriteBatch, new Vector2(VirtualWidth / 2f, 106f), 0.38f, SpriteEffects.None);
        DrawTextCentered(CharacterLabel(_characterSelection), new Vector2(VirtualWidth / 2f, 118f), new Color(214, 191, 135), 0.62f);

        const float primaryScale = 0.43f;
        const float secondaryScale = 0.40f;
        var descriptionBox = new UiRect(14, 125, VirtualWidth - 28, 33);
        var descriptionLayout = TextLayoutEngine.LayoutVerticalStack(
            descriptionBox,
            horizontalPadding: 6f,
            verticalPadding: 1f,
            new[]
            {
                new TextSectionSpec(
                    LocalizedText.Get(CharacterSpecialPrimaryKey()),
                    _font.LineSpacing * primaryScale,
                    0f,
                    s => _font.MeasureString(s).X * primaryScale,
                    JustifyMultiline: true),
                new TextSectionSpec(
                    LocalizedText.Get(CharacterSpecialSecondaryKey()),
                    _font.LineSpacing * secondaryScale,
                    2f,
                    s => _font.MeasureString(s).X * secondaryScale,
                    JustifyMultiline: true)
            });
        DrawTextBlock(descriptionLayout.Sections[0], _font, new Color(156, 201, 191), primaryScale);
        DrawTextBlock(descriptionLayout.Sections[1], _font, Color.White, secondaryScale);

        DrawSelectionButtons();
        EndUi();
    }

    private void DrawDifficultySelection()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawGameplayBackground();
        DrawTextCentered(LocalizedText.Get("SelectDifficulty"), new Vector2(VirtualWidth / 2f, 24f), new Color(214, 191, 135), 0.95f);

        for (var index = 0; index < _difficultyKeys.Length; index++)
        {
            var selected = index == _difficultySelection;
            var bounds = DifficultyButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get(_difficultyKeys[index]), bounds.Center.ToVector2(), Color.White, 0.75f);
        }

        DrawTextCentered($"{LocalizedText.Get("Character")}: {CharacterLabel(_characterSelection)}", new Vector2(VirtualWidth / 2f, 122f), new Color(156, 170, 169), 0.58f);
        DrawSelectionButtons();
        EndUi();
    }

    private void DrawSelectionButtons()
    {
        var continueButton = SelectionContinueButton();
        DrawRect(continueButton, new Color(78, 96, 105));
        DrawRect(continueButton, new Color(214, 191, 135), false, 1);
        DrawTextCentered(LocalizedText.Get("Continue"), continueButton.Center.ToVector2(), Color.White, 0.64f);

        if (IsMobile)
        {
            var backButton = SelectionBackButton();
            DrawRect(backButton, new Color(42, 53, 63));
            DrawRect(backButton, new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get("Back"), backButton.Center.ToVector2(), Color.White, 0.64f);
        }
    }

    private void DrawAct01(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        _level.Draw(gameTime, _spriteBatch, _camera);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: _camera.GetViewMatrix());
        DrawSealAshesVisual();
        foreach (var enemy in _enemies)
        {
            if (enemy.IsDeathAnimationComplete)
                continue;

            enemy.Draw(_spriteBatch);
            if (enemy.IsAlive)
                DrawWorldHealthBar(enemy.Position, enemy.Health, enemy.MaxHealth);
        }
        if (_boss is not null && (_boss.IsAlive || !_boss.IsDeathAnimationComplete))
        {
            _boss.Draw(_spriteBatch);
            if (_boss.IsAlive)
            {
                DrawWorldHealthBar(_boss.Position, _boss.Health, _boss.MaxHealth, boss: true);
                DrawTextCentered(
                    LocalizedText.Get(_boss.LocalizationNameKey),
                    new Vector2(_boss.Position.X, _boss.Position.Y - 91f),
                    new Color(246, 214, 148),
                    0.38f);
            }
        }
        DrawPlayer();
        foreach (var projectile in _projectiles)
            projectile.Draw(_spriteBatch);
        foreach (var projectile in _enemyProjectiles)
            projectile.Draw(_spriteBatch);
        _effects.Draw(_spriteBatch, _whiteTexture);
        _spriteBatch.End();

        DrawAtmosphere();
        BeginUi();
        DrawPlayerHealthBar();
        if (_bossBannerRemaining > 0f && !string.IsNullOrWhiteSpace(_bossBannerText))
            DrawBossBanner();
        if (IsMobile)
        {
            DrawAnalog();
            DrawActionButtons();
            DrawTouchPauseButton();
        }
        EndUi();
        if (_levelBriefingRemaining > 0f)
            DrawLevelBriefingOverlay();
    }

    private void DrawAtmosphere()
    {
        BeginUi();
        if (_currentLevelId == "Act_02_C")
            DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(4, 8, 25, 152));

        if (_currentLevelId == "Act_01_A")
        {
            DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(64, 110, 150, 18));
            for (var index = 0; index < 24; index++)
            {
                var x = (index * 31 + _atmosphereTime * 18f) % (VirtualWidth + 24f) - 12f;
                var y = (index * 17 + _atmosphereTime * 54f) % (VirtualHeight + 20f) - 10f;
                DrawLine(new Vector2(x, y), new Vector2(x - 3f, y + 9f), 1f, new Color(155, 190, 208, 92));
            }
        }

        if (_currentLevelId == "Act_02_B")
        {
            for (var index = 0; index < 14; index++)
            {
                var y = 27f + (index * 19f) % 142f;
                var x = ((index * 47f + _atmosphereTime * 76f) % 390f) - 35f;
                DrawLine(new Vector2(x, y), new Vector2(x + 21f, y - 4f), 1f, new Color(225, 206, 171, 92));
            }
        }

        if (_currentLevelId == "Act_02_C")
        {
            // Purely visual night birds: they never enter the gameplay object list
            // and therefore cannot hit or damage the player.
            for (var index = 0; index < 5; index++)
            {
                var x = ((index * 77f + _atmosphereTime * (18f + index * 2f)) % 380f) - 30f;
                var y = 34f + (index * 23f) % 74f + MathF.Sin(_atmosphereTime * 2f + index) * 4f;
                var wing = 3f + MathF.Sin(_atmosphereTime * 5f + index) * 1.5f;
                var color = new Color(18, 25, 38, 210);
                DrawLine(new Vector2(x - 4f, y + wing), new Vector2(x, y), 1f, color);
                DrawLine(new Vector2(x, y), new Vector2(x + 4f, y + wing), 1f, color);
            }
        }

        if (_currentLevelId is "Act_03_A" or "Act_03_B" or "Act_03_C")
        {
            var tint = _currentLevelId == "Act_03_C"
                ? new Color(18, 9, 14, 72)
                : new Color(24, 14, 12, 34);
            DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), tint);
            for (var index = 0; index < 18; index++)
            {
                var x = ((index * 37f + _atmosphereTime * (12f + (index % 3) * 5f)) % (VirtualWidth + 20f)) - 10f;
                var y = ((index * 23f + _atmosphereTime * (18f + (index % 4) * 4f)) % (VirtualHeight + 16f)) - 8f;
                var emberColor = index % 4 == 0
                    ? new Color(246, 132, 62, 145)
                    : new Color(188, 178, 170, 110);
                DrawRect(new Rectangle((int)x, (int)y, 2, 1), emberColor);
            }
        }

        EndUi();
    }

    private void DrawSealAshesVisual()
    {
        if (!IsBossRewardLevel)
            return;

        foreach (var collectible in _level.Collectibles)
        {
            if (!_collectedCollectibles.Contains(collectible.Name) &&
                IsSealCollectible(collectible) &&
                IsSealCollectibleVisible(collectible))
            {
                DrawSealRubyTile(collectible.Bounds);
            }
        }
    }

    private void DrawSealRubyTile(Rectangle bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var pulse = 0.55f + 0.45f * MathF.Sin(_atmosphereTime * 6f);
        DrawRect(new Rectangle(x - 2, y - 2, bounds.Width + 4, bounds.Height + 4), new Color(255, 214, 130) * (0.45f + pulse * 0.45f), false, 1);
        DrawRect(new Rectangle(x, y, bounds.Width, bounds.Height), new Color(42, 7, 22, 235));
        DrawRect(new Rectangle(x + 2, y + 2, Math.Max(1, bounds.Width - 4), Math.Max(1, bounds.Height - 4)), new Color(145, 12, 36, 245));
        DrawLine(new Vector2(x + 3, y + bounds.Height - 4), new Vector2(x + bounds.Width / 2f, y + 3), 2f, new Color(255, 86, 97, 245));
        DrawLine(new Vector2(x + bounds.Width / 2f, y + 3), new Vector2(x + bounds.Width - 3, y + bounds.Height - 4), 2f, new Color(95, 4, 28, 245));
        DrawRect(new Rectangle(x + 5, y + 4, 3, 3), new Color(255, 218, 190, 235));
    }

    private void DrawPlayer()
    {
        var effects = _playerFacing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _player.Draw(_spriteBatch, _playerPosition, PlayerScale, effects);
    }

    private void DrawPlayerHealthBar()
    {
        var characterName = CharacterLabel(_characterSelection);
        DrawText(characterName, new Vector2(8f, 5f), Color.White, 0.48f);
        var miniStartX = 8f + _font.MeasureString(characterName).X * 0.48f + 4f;
        for (var index = 0; index < _livesRemaining; index++)
        {
            _player.Draw(
                _spriteBatch,
                new Vector2(miniStartX + index * 9f, 15f),
                0.16f,
                SpriteEffects.None);
        }
        DrawHealthBar(new Rectangle(8, 17, 92, 7), _playerHealth, CombatBalance.GetPlayerMaxHealth(_selectedDifficulty));
        DrawText(LocalizedText.Get("Stamina"), new Vector2(196f, 5f), Color.White, 0.46f);
        DrawHealthBar(new Rectangle(196, 17, 72, 7), (int)Math.Round(_stamina), (int)StaminaMaximum);
        DrawText(LocalizedText.Get("Power"), new Vector2(110f, 5f), Color.White, 0.46f);
        DrawHealthBar(new Rectangle(110, 17, 72, 7), (int)Math.Round(_power), CombatBalance.PlayerPowerMaximum);
        var sealText = string.Format(
            CultureInfo.CurrentCulture,
            LocalizedText.Get("AshSealHud"),
            Math.Clamp(_saveProgress.SealAshesParts, 0, 4));
        DrawText(sealText, new Vector2(272f, 5f), new Color(235, 198, 128), 0.36f);
    }

    private void DrawBossBanner()
    {
        var banner = new Rectangle(52, 30, VirtualWidth - 104, 18);
        DrawRect(banner, new Color(18, 12, 22, 225));
        DrawRect(banner, new Color(214, 191, 135), false, 1);
        DrawTextCentered(_bossBannerText, banner.Center.ToVector2(), new Color(255, 226, 154), _fontSemibold, new Vector2(0.56f, 0.56f));
    }

    private void DrawLevelBriefingOverlay()
    {
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(4, 7, 12, 220));

        var panel = new Rectangle(16, 12, VirtualWidth - 32, VirtualHeight - 24);
        DrawRect(panel, new Color(25, 35, 45));
        DrawRect(panel, new Color(214, 191, 135), false, 2);

        const float titleScale = 0.68f;
        const float headingScale = 0.50f;
        const float objectiveScale = 0.54f;
        const float tutorialScale = 0.46f;
        const float promptScale = 0.48f;

        var promptAreaHeight = 18;
        var contentRect = new UiRect(panel.Left, panel.Top, panel.Width, panel.Height - promptAreaHeight);
        var layout = TextLayoutEngine.LayoutVerticalStack(
            contentRect,
            horizontalPadding: 12f,
            verticalPadding: 8f,
            new[]
            {
                new TextSectionSpec(
                    LocalizedText.Get(_level.Definition.BriefingTitleKey),
                    _fontSemibold.LineSpacing * titleScale,
                    0f,
                    s => _fontSemibold.MeasureString(s).X * titleScale,
                    JustifyMultiline: false),
                new TextSectionSpec(
                    LocalizedText.Get("Objective"),
                    _font.LineSpacing * headingScale,
                    6f,
                    s => _font.MeasureString(s).X * headingScale,
                    JustifyMultiline: false),
                new TextSectionSpec(
                    LocalizedText.Get(_level.Definition.BriefingObjectiveKey),
                    _font.LineSpacing * objectiveScale,
                    2f,
                    s => _font.MeasureString(s).X * objectiveScale,
                    JustifyMultiline: true),
                new TextSectionSpec(
                    LocalizedText.Get("Tutorial"),
                    _font.LineSpacing * headingScale,
                    6f,
                    s => _font.MeasureString(s).X * headingScale,
                    JustifyMultiline: false),
                new TextSectionSpec(
                    LocalizedText.Get(_level.Definition.BriefingTutorialKey),
                    _font.LineSpacing * tutorialScale,
                    2f,
                    s => _font.MeasureString(s).X * tutorialScale,
                    JustifyMultiline: true)
            });

        DrawTextBlock(layout.Sections[0], _fontSemibold, new Color(214, 191, 135), titleScale);
        DrawTextBlock(layout.Sections[1], _font, new Color(130, 190, 177), headingScale);
        DrawTextBlock(layout.Sections[2], _font, Color.White, objectiveScale);
        DrawTextBlock(layout.Sections[3], _font, new Color(130, 190, 177), headingScale);
        DrawTextBlock(layout.Sections[4], _font, Color.White, tutorialScale);

        var promptRect = new UiRect(panel.Left, panel.Bottom - promptAreaHeight, panel.Width, promptAreaHeight - 2);
        var promptLayout = TextLayoutEngine.LayoutVerticalStack(
            promptRect,
            horizontalPadding: 12f,
            verticalPadding: 2f,
            new[]
            {
                new TextSectionSpec(
                    LocalizedText.Get("LevelBriefingPrompt"),
                    _font.LineSpacing * promptScale,
                    0f,
                    s => _font.MeasureString(s).X * promptScale,
                    JustifyMultiline: false)
            });
        DrawTextBlock(promptLayout.Sections[0], _font, new Color(214, 191, 135), promptScale);
        EndUi();
    }

    private void DrawPauseOverlay()
    {
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(4, 7, 12, 205));
        DrawRect(new Rectangle(24, 10, VirtualWidth - 48, VirtualHeight - 20), new Color(20, 29, 39, 245));
        DrawRect(new Rectangle(24, 10, VirtualWidth - 48, VirtualHeight - 20), new Color(214, 191, 135), false, 2);
        DrawTextCentered(LocalizedText.Get("Paused"), new Vector2(VirtualWidth / 2f, 21f), new Color(214, 191, 135), _fontSemiboldWide, 0.9f);

        DrawPauseButton(PauseResumeButton(), _pauseSelection == PauseResumeIndex, LocalizedText.Get("Resume"));
        DrawPauseButton(PauseContinueButton(), _pauseSelection == PauseContinueIndex, LocalizedText.Get("ContinueFromState"));
        DrawText(LocalizedText.Get("MemoryCard"), new Vector2(42f, 61f), new Color(130, 190, 177), 0.42f);

        var anyOccupied = false;
        for (var slot = 1; slot <= SaveProgressStore.SlotCount; slot++)
        {
            var occupied = SaveProgressStore.HasSlot(slot);
            if (occupied)
                anyOccupied = true;

            var slotLabel = occupied
                ? string.Format(CultureInfo.CurrentCulture, LocalizedText.Get("SlotOccupied"), slot)
                : string.Format(CultureInfo.CurrentCulture, LocalizedText.Get("SlotEmpty"), slot);
            var selected = _pauseSelection == PauseSaveSlotStartIndex + slot - 1;
            var holdProgress = _pauseSlotInput.HoldingSlot == slot ? _pauseSlotInput.HoldProgress : 0f;
            DrawSaveSlotButton(
                PauseSlotButton(slot),
                selected,
                occupied,
                slotLabel,
                holdProgress);
        }

        DrawPauseButton(PauseOptionsButton(), _pauseSelection == PauseOptionsIndex, LocalizedText.Get("Options"));
        DrawPauseButton(PauseMainMenuButton(), _pauseSelection == PauseMainMenuIndex, LocalizedText.Get("MainMenu"));

        var selectedSlot = GetSelectedPauseSlot();
        var showDeleteHint = (selectedSlot.HasValue && SaveProgressStore.HasSlot(selectedSlot.Value)) ||
                             (IsMobile && anyOccupied);
        if (!string.IsNullOrWhiteSpace(_pauseStatusMessage))
        {
            DrawTextCentered(_pauseStatusMessage, new Vector2(VirtualWidth / 2f, 168f), new Color(156, 201, 191), 0.42f);
        }
        else if (showDeleteHint)
        {
            var hintKey = IsMobile ? "DeleteSlotHintMobile" : "DeleteSlotHintDesktop";
            DrawTextCentered(LocalizedText.Get(hintKey), new Vector2(VirtualWidth / 2f, 168f), new Color(236, 188, 120), 0.40f);
        }
        else
        {
            DrawTextCentered(LocalizedText.Get("PausePrompt"), new Vector2(VirtualWidth / 2f, 168f), Color.White, 0.42f);
        }
        EndUi();
    }

    private void DrawSaveSlotSelectionOverlay(bool paused)
    {
        if (!paused)
            GraphicsDevice.Clear(new Color(12, 16, 24));

        BeginUi();
        if (paused)
        {
            DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(4, 7, 12, 205));
            DrawRect(new Rectangle(24, 10, VirtualWidth - 48, VirtualHeight - 20), new Color(20, 29, 39, 245));
            DrawRect(new Rectangle(24, 10, VirtualWidth - 48, VirtualHeight - 20), new Color(214, 191, 135), false, 2);
        }
        else
        {
            DrawMenuBackground();
            DrawRect(new Rectangle(12, 12, VirtualWidth - 24, VirtualHeight - 24), new Color(25, 35, 45), false, 2);
        }

        DrawTextCentered(LocalizedText.Get("ContinueFromState"), new Vector2(VirtualWidth / 2f, 25f), new Color(214, 191, 135), _fontSemiboldWide, 0.72f);
        DrawTextCentered(LocalizedText.Get("SelectSaveSlot"), new Vector2(VirtualWidth / 2f, 39f), new Color(156, 201, 191), 0.42f);

        var anyOccupied = false;
        for (var slot = 1; slot <= SaveProgressStore.SlotCount; slot++)
        {
            var occupied = SaveProgressStore.HasSlot(slot);
            if (occupied)
                anyOccupied = true;

            var slotLabel = occupied
                ? string.Format(CultureInfo.CurrentCulture, LocalizedText.Get("SlotOccupied"), slot)
                : string.Format(CultureInfo.CurrentCulture, LocalizedText.Get("SlotEmpty"), slot);
            var selected = _slotSelection == slot - 1;
            var holdProgress = _loadSlotInput.HoldingSlot == slot ? _loadSlotInput.HoldProgress : 0f;
            DrawSaveSlotButton(
                SaveSlotButton(slot),
                selected,
                occupied,
                slotLabel,
                holdProgress);
        }

        DrawPauseButton(SaveSlotBackButton(), _slotSelection == SaveProgressStore.SlotCount, LocalizedText.Get("Back"));

        var selectedSlot = _slotSelection < SaveProgressStore.SlotCount ? _slotSelection + 1 : (int?)null;
        var showDeleteHint = (selectedSlot.HasValue && SaveProgressStore.HasSlot(selectedSlot.Value)) ||
                             (IsMobile && anyOccupied);
        if (!string.IsNullOrWhiteSpace(_slotStatusMessage))
        {
            DrawTextCentered(_slotStatusMessage, new Vector2(VirtualWidth / 2f, 161f), new Color(236, 188, 120), 0.42f);
        }
        else if (showDeleteHint)
        {
            var hintKey = IsMobile ? "DeleteSlotHintMobile" : "DeleteSlotHintDesktop";
            DrawTextCentered(LocalizedText.Get(hintKey), new Vector2(VirtualWidth / 2f, 161f), new Color(236, 188, 120), 0.38f);
        }
        else
        {
            DrawTextCentered(LocalizedText.Get("PausePrompt"), new Vector2(VirtualWidth / 2f, 161f), Color.White, 0.38f);
        }
        EndUi();
    }

    private void DrawPauseButton(Rectangle bounds, bool selected, string label)
    {
        DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(label, bounds.Center.ToVector2(), Color.White, 0.48f);
    }

    private void DrawSaveSlotButton(Rectangle bounds, bool selected, bool occupied, string label, float holdProgress)
    {
        DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
        if (holdProgress > 0f)
        {
            var fillWidth = Math.Clamp((int)Math.Round(bounds.Width * holdProgress), 1, bounds.Width);
            DrawRect(new Rectangle(bounds.Left, bounds.Top, fillWidth, bounds.Height), new Color(176, 56, 52, 175));
        }

        DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(label, bounds.Center.ToVector2(), Color.White, 0.48f);

        if (IsDesktop && selected && occupied)
        {
            var badgeText = LocalizedText.Get("SlotDeleteBadge");
            var badgeWidth = Math.Max(22, (int)Math.Ceiling(_font.MeasureString(badgeText).X * 0.38f) + 6);
            var badgeHeight = Math.Max(9, bounds.Height - 4);
            var badgeRect = new Rectangle(
                bounds.Right - badgeWidth - 3,
                bounds.Top + (bounds.Height - badgeHeight) / 2,
                badgeWidth,
                badgeHeight);
            DrawRect(badgeRect, new Color(138, 44, 42, 230));
            DrawRect(badgeRect, new Color(236, 188, 120), false, 1);
            DrawTextCentered(badgeText, badgeRect.Center.ToVector2(), new Color(255, 236, 210), 0.38f);
        }
    }

    private void DrawWorldHealthBar(Vector2 feetPosition, int health, int maximumHealth, bool boss = false)
    {
        var bounds = new Rectangle((int)feetPosition.X - (boss ? 28 : 16), (int)feetPosition.Y - (boss ? 84 : 40), boss ? 56 : 32, boss ? 5 : 4);
        DrawHealthBar(bounds, health, maximumHealth);
    }

    private void DrawHealthBar(Rectangle bounds, int health, int maximumHealth)
    {
        var safeMaximum = Math.Max(1, maximumHealth);
        var safeHealth = Math.Clamp(health, 0, safeMaximum);
        DrawRect(bounds, new Color(42, 25, 30, 230));
        var fillWidth = (int)Math.Round(bounds.Width * safeHealth / (float)safeMaximum);
        if (fillWidth > 0)
            DrawRect(new Rectangle(bounds.Left, bounds.Top, fillWidth, bounds.Height), safeHealth > safeMaximum / 3 ? new Color(76, 177, 84) : new Color(204, 70, 64));
        DrawRect(bounds, new Color(214, 191, 135), false, 1);
    }

    private void DrawAnalog()
    {
        var center = AnalogCenter();
        DrawSolidCircle(center, AnalogRadius, new Color(20, 28, 34, 190));
        DrawCircle(center, AnalogRadius, new Color(123, 145, 145), 2f);
        var knob = center + _analogVisualInput * 18f;
        DrawSolidCircle(knob, 9f, new Color(91, 112, 116, 230));
        DrawCircle(knob, 9f, new Color(214, 191, 135), 1f);
    }

    private void DrawActionButtons()
    {
        var buttons = new[]
        {
            (new Rectangle(236, 132, 24, 24), "A"),
            (new Rectangle(264, 132, 24, 24), "B"),
            (new Rectangle(292, 132, 24, 24), "C"),
            (new Rectangle(236, 104, 24, 24), "X"),
            (new Rectangle(264, 104, 24, 24), "Y"),
            (new Rectangle(292, 104, 24, 24), "Z")
        };
        foreach (var (bounds, label) in buttons)
        {
            DrawSolidCircle(bounds.Center.ToVector2(), Math.Min(bounds.Width, bounds.Height) / 2f, new Color(65, 78, 85, 230));
            DrawCircle(bounds.Center.ToVector2(), Math.Min(bounds.Width, bounds.Height) / 2f, new Color(214, 191, 135), 1f);
            DrawTextCentered(label, bounds.Center.ToVector2(), Color.White, 0.75f);
        }
    }

    private void DrawTouchPauseButton()
    {
        var bounds = PauseTouchButton();
        DrawRect(bounds, new Color(42, 53, 63, 230));
        DrawRect(bounds, new Color(214, 191, 135), false, 1);
        DrawTextCentered("II", bounds.Center.ToVector2(), Color.White, 0.52f);
    }

    private void BeginUi()
    {
        // UI fonts are rendered at the virtual resolution and then scaled to the
        // window. Linear sampling prevents a horizontal atlas seam from cutting
        // through the Array glyphs while preserving the pixel-art sampler for
        // the world and character sprites.
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, transformMatrix: _viewportAdapter.GetScaleMatrix());
    }

    private void EndUi() => _spriteBatch.End();

    private void DrawText(string text, Vector2 position, Color color, float scale)
    {
        _spriteBatch.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawTextCentered(string text, Vector2 center, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        DrawText(text, center - size / 2f, color, scale);
    }

    private void DrawTextCentered(string text, Vector2 center, Color color, SpriteFont font, float scale)
    {
        var size = font.MeasureString(text);
        _spriteBatch.DrawString(font, text, center, color, 0f, size / 2f, scale, SpriteEffects.None, 0f);
    }

    private void DrawTextCentered(string text, Vector2 center, Color color, SpriteFont font, Vector2 scale)
    {
        var size = font.MeasureString(text);
        _spriteBatch.DrawString(font, text, center, color, 0f, size / 2f, scale, SpriteEffects.None, 0f);
    }

    private void DrawTextBlock(TextBlockLayout block, SpriteFont font, Color color, float scale)
    {
        foreach (var line in block.Lines)
        {
            foreach (var word in line.Words)
            {
                _spriteBatch.DrawString(
                    font,
                    word.Text,
                    new Vector2(word.X, word.Y),
                    color,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    private void DrawRect(Rectangle bounds, Color color, bool filled = true, int thickness = 1)
    {
        if (filled)
        {
            _spriteBatch.Draw(_whiteTexture, bounds, color);
            return;
        }

        _spriteBatch.Draw(_whiteTexture, new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness), color);
        _spriteBatch.Draw(_whiteTexture, new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness), color);
        _spriteBatch.Draw(_whiteTexture, new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height), color);
        _spriteBatch.Draw(_whiteTexture, new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height), color);
    }

    private void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        var delta = to - from;
        var length = delta.Length();
        var angle = (float)Math.Atan2(delta.Y, delta.X);
        _spriteBatch.Draw(_whiteTexture, from, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private void DrawCircle(Vector2 center, float radius, Color color, float thickness)
    {
        const int segments = 24;
        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= segments; index++)
        {
            var angle = MathHelper.TwoPi * index / segments;
            var next = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            DrawLine(previous, next, thickness, color);
            previous = next;
        }
    }

    private void DrawSolidCircle(Vector2 center, float radius, Color color)
    {
        var diameter = (int)Math.Ceiling(radius * 2f);
        for (var y = 0; y < diameter; y++)
        {
            var dy = y - radius + 0.5f;
            var halfWidth = (int)Math.Sqrt(Math.Max(0f, radius * radius - dy * dy));
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)center.X - halfWidth, (int)(center.Y + dy), halfWidth * 2, 1), color);
        }
    }

    private Rectangle MenuButton(int index) => new(80, 52 + index * 25, 160, 21);

    private Rectangle PauseTouchButton() => new(278, 4, 34, 16);

    private Rectangle PauseResumeButton() => new(64, 28, 192, 14);

    private Rectangle PauseContinueButton() => new(64, 44, 192, 14);

    private Rectangle PauseSlotButton(int slot) => new(42, 66 + (slot - 1) * 14, 236, 13);

    private Rectangle PauseOptionsButton() => new(64, 113, 192, 14);

    private Rectangle PauseMainMenuButton() => new(64, 130, 192, 14);

    private Rectangle SaveSlotButton(int slot) => new(42, 55 + (slot - 1) * 22, 236, 18);

    private Rectangle SaveSlotBackButton() => new(112, 125, 96, 18);

    private Rectangle LanguageSelectorBounds() => new(38, 46, 244, 22);

    private Rectangle LanguagePreviousArrowButton()
    {
        var selector = LanguageSelectorBounds();
        return new Rectangle(selector.Left, selector.Top, 26, selector.Height);
    }

    private Rectangle LanguageNextArrowButton()
    {
        var selector = LanguageSelectorBounds();
        return new Rectangle(selector.Right - 26, selector.Top, 26, selector.Height);
    }

    private Rectangle LanguageSelectorSwipeArea()
    {
        var selector = LanguageSelectorBounds();
        return new Rectangle(selector.Left - 14, selector.Top - 8, selector.Width + 28, selector.Height + 18);
    }

    private Rectangle CharacterSelectionButton(int index) => new(8 + index * 104, 49, 96, 22);

    private Rectangle DifficultyButton(int index) => new(72, 50 + index * 24, 176, 20);

    private Rectangle SelectionContinueButton() => new(112, 160, 96, 18);

    private Rectangle SelectionBackButton() => new(8, 160, 72, 18);

    private Rectangle OptionsBackButton() => new(112, 152, 96, 16);

    private Rectangle ControlsButton() => new(98, 80, 124, 15);

    private Rectangle FullScreenButton() => new(16, 98, 288, 14);

    private Rectangle BgmVolumeButton() => IsDesktop
        ? new Rectangle(16, 115, 288, 14)
        : new Rectangle(16, 82, 288, 16);

    private Rectangle SfxVolumeButton() => IsDesktop
        ? new Rectangle(16, 132, 288, 14)
        : new Rectangle(16, 102, 288, 16);

    private Rectangle MuteButton() => IsDesktop
        ? new Rectangle(16, 149, 288, 14)
        : new Rectangle(16, 122, 288, 16);

    private Rectangle ControlsKeyboardButton() => new(8, 27, 148, 14);

    private Rectangle ControlsGamepadButton() => new(164, 27, 148, 14);

    private Rectangle ControlRow(int index) => new(16, 42 + index * 14, 288, 13);

    private Rectangle ControlsBackButton() => new(112, 166, 96, 11);

    private bool IsKeyboardMoveDown(KeyboardState keyboard, int index)
    {
        var fallback = index == 0 ? Keys.Left : Keys.Right;
        return keyboard.IsKeyDown(_keyboardBindings[index]) || keyboard.IsKeyDown(fallback);
    }

    private string GetKeyboardBindingLabel(int index) => _keyboardBindings[index].ToString();

    private string GetGamepadBindingLabel(int index) => _gamepadBindings[index] switch
    {
        Buttons.LeftShoulder => "L Shoulder",
        Buttons.RightShoulder => "R Shoulder",
        _ => _gamepadBindings[index].ToString()
    };

    private string CharacterLabel(int index) => _characterIds[index] switch
    {
        "Kunoichi" => LocalizedText.Get("Kunoichi"),
        "Ninja_Monk" => LocalizedText.Get("Monk"),
        _ => LocalizedText.Get("Peasant")
    };

    private string CharacterSpecialPrimaryKey() => _characterIds[_characterSelection] switch
    {
        "Kunoichi" => "KunoichiSpecialPrimary",
        "Ninja_Monk" => "MonkSpecialPrimary",
        _ => "PeasantSpecialPrimary"
    };

    private string CharacterSpecialSecondaryKey() => _characterIds[_characterSelection] switch
    {
        "Kunoichi" => "KunoichiSpecialSecondary",
        "Ninja_Monk" => "MonkSpecialSecondary",
        _ => "PeasantSpecialSecondary"
    };

    private const float AnalogRadius = 24f;

    private Vector2 AnalogCenter() => new(38f, 145f);

    private Rectangle ActionButtonBounds(int index) => index switch
    {
        0 => new Rectangle(236, 132, 24, 24),
        1 => new Rectangle(264, 132, 24, 24),
        2 => new Rectangle(292, 132, 24, 24),
        3 => new Rectangle(236, 104, 24, 24),
        4 => new Rectangle(264, 104, 24, 24),
        _ => new Rectangle(292, 104, 24, 24)
    };

    private Point? GetPressedPoint(TouchCollection touches)
    {
        foreach (var touch in touches)
        {
            if (touch.State is TouchLocationState.Pressed or TouchLocationState.Moved or TouchLocationState.Released)
                return ToVirtualPoint(touch.Position);
        }

        var mouse = Mouse.GetState();
        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
            return ToVirtualPoint(mouse.Position.ToVector2());

        return null;
    }

    private SlotTouchInput[] ConvertTouchesToVirtual(TouchCollection touches)
    {
        if (touches.Count == 0)
            return Array.Empty<SlotTouchInput>();

        var result = new SlotTouchInput[touches.Count];
        for (var i = 0; i < touches.Count; i++)
        {
            var touch = touches[i];
            var vp = ToVirtualPoint(touch.Position);
            result[i] = new SlotTouchInput(touch.Id, ToTouchPhase(touch.State), new UiPoint(vp.X, vp.Y));
        }

        return result;
    }

    private static TouchPhase ToTouchPhase(TouchLocationState state) => state switch
    {
        TouchLocationState.Pressed => TouchPhase.Pressed,
        TouchLocationState.Moved => TouchPhase.Moved,
        TouchLocationState.Released => TouchPhase.Released,
        _ => TouchPhase.Invalid
    };

    private static UiRect ToUiRect(Rectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static UiPoint? ToUiPointNullable(Point? point) =>
        point.HasValue ? new UiPoint(point.Value.X, point.Value.Y) : null;

    private static Point? GetFreshTouchPressPoint(IReadOnlyList<SlotTouchInput> touches)
    {
        foreach (var touch in touches)
        {
            if (touch.State == TouchPhase.Pressed)
                return new Point(touch.Position.X, touch.Position.Y);
        }

        return null;
    }

    private Point? GetFreshLeftClickPoint()
    {
        var mouse = Mouse.GetState();
        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
            return ToVirtualPoint(mouse.Position.ToVector2());

        return null;
    }

    private Point? GetFreshRightClickPoint()
    {
        var mouse = Mouse.GetState();
        if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
            return ToVirtualPoint(mouse.Position.ToVector2());

        return null;
    }

    private bool IsNewTouchPress(TouchCollection touches, Rectangle bounds)
    {
        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Pressed && bounds.Contains(ToVirtualPoint(touch.Position)))
                return true;
        }

        return false;
    }

    private Point ToVirtualPoint(Vector2 screenPosition)
    {
        var viewport = GraphicsDevice.Viewport;
        // TouchPanel positions are already expressed in back-buffer/viewport
        // coordinates. Viewport.X/Y are only the render letterbox offset and
        // must not be subtracted from touch input a second time.
        var x = screenPosition.X * VirtualWidth / Math.Max(1, viewport.Width);
        var y = screenPosition.Y * VirtualHeight / Math.Max(1, viewport.Height);

        return new Point((int)x, (int)y);
    }

    private bool IsNewKeyPress(KeyboardState current, Keys key) => current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private bool IsNewButtonPress(GamePadState current, Buttons button) =>
        current.IsButtonDown(button) && !_previousGamePad.IsButtonDown(button);

    private bool IsNewGamepadUp(GamePadState current) =>
        (current.DPad.Up == ButtonState.Pressed && _previousGamePad.DPad.Up == ButtonState.Released) ||
        (current.ThumbSticks.Left.Y > 0.5f && _previousGamePad.ThumbSticks.Left.Y <= 0.5f);

    private bool IsNewGamepadDown(GamePadState current) =>
        (current.DPad.Down == ButtonState.Pressed && _previousGamePad.DPad.Down == ButtonState.Released) ||
        (current.ThumbSticks.Left.Y < -0.5f && _previousGamePad.ThumbSticks.Left.Y >= -0.5f);

    private bool IsNewGamepadLeft(GamePadState current) =>
        (current.DPad.Left == ButtonState.Pressed && _previousGamePad.DPad.Left == ButtonState.Released) ||
        (current.ThumbSticks.Left.X < -0.5f && _previousGamePad.ThumbSticks.Left.X >= -0.5f);

    private bool IsNewGamepadRight(GamePadState current) =>
        (current.DPad.Right == ButtonState.Pressed && _previousGamePad.DPad.Right == ButtonState.Released) ||
        (current.ThumbSticks.Left.X > 0.5f && _previousGamePad.ThumbSticks.Left.X <= 0.5f);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _audio?.Dispose();

        base.Dispose(disposing);
    }

    private enum GameScreen
    {
        Intro,
        MainMenu,
        Options,
        PlayerSelection,
        DifficultySelection,
        Controls,
        Act01,
        Paused,
        SaveSlotSelection,
        GameOver,
        Victory,
        Credits
    }

    private enum BindingDevice
    {
        Keyboard,
        Gamepad
    }

}
