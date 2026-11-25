using System;
using System.Collections.Generic;
using KuroganesEcho.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace KuroganesEcho.Core;

/// <summary>
/// Minimal playable shell for the first visual test: localized menu, options and
/// a read-only-action movement test inside the Act 01 Tiled map.
/// </summary>
public class KuroganesEchoGame : Game
{
    private const int VirtualWidth = 320;
    private const int VirtualHeight = 180;
    private const float PlayerSpeed = 72f;
    private const float RunSpeed = 132f;
    private const float PlayerScale = 0.5f;
    private const float Gravity = 480f;
    private const float JumpVelocity = -180f;
    private const float PlayerHalfWidth = 10f;
    private const int MaximumJumps = 2;
    private const float PlayerHeight = 32f;
    private const float IntroScrollSpeed = 8f;
    private const float IntroLineHeight = 13f;
    private static readonly string[] IntroLines =
    {
        "O clã Kurogane guardava o",
        "Selo de Cinzas há três gerações.",
        "",
        "Um talismã de ferro forjado",
        "com o sangue e a memória dos",
        "antigos mestres ninjas.",
        "",
        "A relíquia mantém acesa a",
        "Chama Silenciosa, um vínculo",
        "espiritual que impede os espíritos",
        "sem descanso e os guardiões",
        "corrompidos das montanhas de",
        "invadirem a vila do clã."
    };

    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _languageCodes = { "pt-BR", "en-US", "fr-FR" };
    private readonly string[] _menuKeys = { "Play", "Options", "Quit" };
    private readonly string[] _difficultyKeys = { "Easy", "Normal", "Hard" };
    private readonly string[] _controlKeys = { "MoveLeft", "MoveRight", "Attack1", "Attack2", "Jump", "Run" };
    private readonly Keys[] _keyboardBindings = { Keys.A, Keys.D, Keys.Z, Keys.X, Keys.C, Keys.R };
    private readonly Buttons[] _gamepadBindings = { Buttons.DPadLeft, Buttons.DPadRight, Buttons.A, Buttons.B, Buttons.X, Buttons.Y };
    private static readonly Buttons[] ConfigurableGamepadButtons =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.Start, Buttons.Back
    };

    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private SpriteFont _fontSemibold = null!;
    private SpriteFont _fontWide = null!;
    private SpriteFont _fontSemiboldWide = null!;
    private Texture2D _whiteTexture = null!;
    private PlayerCharacter _player = null!;
    private GameLevel _level = null!;
    private AudioService _audio = null!;
    private readonly List<EnemyInstance> _enemies = new();
    private BoxingViewportAdapter _viewportAdapter = null!;
    private OrthographicCamera _camera = null!;
    private readonly string[] _characterIds = { "Kunoichi", "Ninja_Monk", "Ninja_Peasant" };

    private GameScreen _screen = GameScreen.Intro;
    private float _introScroll;
    private int _menuSelection;
    private int _languageSelection;
    private int _optionsSelection;
    private int _characterSelection;
    private int _difficultySelection = 1;
    private GameDifficulty _selectedDifficulty = GameDifficulty.Normal;
    private int _controlSelection;
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
    private float _playerInvulnerability;
    private float _playerKnockbackVelocity;
    private int _jumpsUsed;
    private int _playerHealth = CombatBalance.PlayerMaxHealth;
    private int _playerFacing = 1;
    private bool _playerAttackHitApplied;
    private KeyboardState _previousKeyboard;
    private GamePadState _previousGamePad;
    private bool _gamepadConnected;
    private MouseState _previousMouse;

    public static bool IsMobile => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
    public static bool IsDesktop => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    public KuroganesEchoGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = false
        };

        Services.AddService(typeof(GraphicsDeviceManager), _graphics);
        Content.RootDirectory = "Content";
        _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
    }

    protected override void Initialize()
    {
        LocalizationManager.SetCulture(_languageCodes[_languageSelection]);
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
        _level = GameLevel.Load(GraphicsDevice, LevelDefinition.Act01);
        _audio = AudioService.Load(Content);
        _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
        LoadEnemies();

        _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        _whiteTexture.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.IndependentAxes);
        _gamepadConnected = gamePad.IsConnected;
        var backPressed = IsNewKeyPress(keyboard, Keys.Escape) || IsNewButtonPress(gamePad, Buttons.Back);
        var touchState = TouchPanel.GetState();

        if (backPressed)
        {
            if (_screen == GameScreen.MainMenu)
                Exit();
            else if (_screen == GameScreen.DifficultySelection)
                _screen = GameScreen.PlayerSelection;
            else if (_screen == GameScreen.PlayerSelection)
                _screen = GameScreen.MainMenu;
            else if (_screen == GameScreen.Controls)
                _screen = GameScreen.Options;
            else
                _screen = GameScreen.MainMenu;
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
        else
            DrawMainMenu();

        base.Draw(gameTime);
    }

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

    private void UpdateIntro(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        _introScroll += (float)gameTime.ElapsedGameTime.TotalSeconds * IntroScrollSpeed;
        var introFinished = _introScroll >= VirtualHeight + IntroLines.Length * IntroLineHeight + 24f;
        var skipRequested = IsNewKeyPress(keyboard, Keys.Enter) ||
                            IsNewKeyPress(keyboard, Keys.Space) ||
                            IsNewButtonPress(gamePad, Buttons.A) ||
                            GetPressedPoint(touches).HasValue;

        if (introFinished || skipRequested)
        {
            _introScroll = 0f;
            _screen = GameScreen.MainMenu;
        }
    }

    private void ActivateMainMenuSelection()
    {
        switch (_menuSelection)
        {
            case 0:
                _screen = GameScreen.PlayerSelection;
                break;
            case 1:
                _optionsSelection = _languageSelection;
                _screen = GameScreen.Options;
                break;
            case 2:
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
            AdjustSelectedAudioOption(-1);
        if (IsNewKeyPress(keyboard, Keys.Right) || IsNewGamepadRight(gamePad))
            AdjustSelectedAudioOption(1);

        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            for (var index = 0; index < _languageCodes.Length; index++)
            {
                if (LanguageButton(index).Contains(point.Value))
                {
                    _optionsSelection = index;
                    SelectLanguage(index);
                    return;
                }
            }

            if (OptionsBackButton().Contains(point.Value))
            {
                _screen = GameScreen.MainMenu;
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
                return;
            }

            if (IsDesktop && ControlsButton().Contains(point.Value))
            {
                _optionsSelection = ControlsOptionIndex;
                _screen = GameScreen.Controls;
                return;
            }

        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space) || IsNewButtonPress(gamePad, Buttons.A))
            ActivateOptionsSelection();
        if (IsNewButtonPress(gamePad, Buttons.B))
            _screen = GameScreen.MainMenu;
    }

    private int ControlsOptionIndex => _languageCodes.Length;

    private int BgmVolumeOptionIndex => _languageCodes.Length + (IsDesktop ? 1 : 0);

    private int SfxVolumeOptionIndex => BgmVolumeOptionIndex + 1;

    private int MuteOptionIndex => SfxVolumeOptionIndex + 1;

    private int BackOptionIndex => MuteOptionIndex + 1;

    private int OptionsOptionCount => BackOptionIndex + 1;

    private void MoveOptionsSelection(int direction)
    {
        var optionCount = OptionsOptionCount;
        _optionsSelection = (_optionsSelection + direction + optionCount) % optionCount;
        if (_optionsSelection < _languageCodes.Length)
            _languageSelection = _optionsSelection;
    }

    private void AdjustSelectedAudioOption(int direction)
    {
        const float step = 0.1f;
        if (_optionsSelection == BgmVolumeOptionIndex)
            _audio.SetBgmVolume(_audio.BgmVolume + direction * step);
        else if (_optionsSelection == SfxVolumeOptionIndex)
            _audio.SetSfxVolume(_audio.SfxVolume + direction * step);
        else if (_optionsSelection == MuteOptionIndex)
            _audio.ToggleMute();
    }

    private void SetAudioVolumeFromPoint(Rectangle bounds, Point point, bool bgm)
    {
        var volume = Math.Clamp((point.X - bounds.Left) / (float)bounds.Width, 0f, 1f);
        if (bgm)
            _audio.SetBgmVolume(volume);
        else
            _audio.SetSfxVolume(volume);
    }

    private void ActivateOptionsSelection()
    {
        if (_optionsSelection < _languageCodes.Length)
        {
            SelectLanguage(_optionsSelection);
            return;
        }

        if (IsDesktop && _optionsSelection == ControlsOptionIndex)
        {
            _screen = GameScreen.Controls;
            return;
        }

        if (_optionsSelection == MuteOptionIndex)
        {
            _audio.ToggleMute();
            return;
        }

        if (_optionsSelection == BackOptionIndex)
            _screen = GameScreen.MainMenu;
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

            if (ControlsBackButton().Contains(point.Value))
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

            if (SelectionBackButton().Contains(point.Value))
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

            if (SelectionBackButton().Contains(point.Value))
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
        _level.ConfigureDifficulty(_selectedDifficulty);
        StartAct01();
    }

    private void SelectLanguage(int index)
    {
        _languageSelection = Math.Clamp(index, 0, _languageCodes.Length - 1);
        LocalizationManager.SetCulture(_languageCodes[_languageSelection]);
    }

    private void SelectCharacter(int index)
    {
        _characterSelection = (index + _characterIds.Length) % _characterIds.Length;
        if (_player is not null)
            _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
    }

    private void StartAct01()
    {
        _screen = GameScreen.Act01;
        _playerPosition = _level.PlayerSpawn;
        _moveInput = Vector2.Zero;
        _analogVisualInput = Vector2.Zero;
        _verticalVelocity = 0f;
        _isGrounded = true;
        _jumpsUsed = 0;
        _runRemaining = 0f;
        _playerHealth = CombatBalance.PlayerMaxHealth;
        _playerInvulnerability = 0f;
        _playerKnockbackVelocity = 0f;
        _playerFacing = 1;
        _playerAttackHitApplied = false;
        _player.Reset();
        foreach (var enemy in _enemies)
            enemy.Reset();
        UpdateCamera();
    }

    private void LoadEnemies()
    {
        _enemies.Clear();
        foreach (var spawn in _level.EnemySpawns)
        {
            var enemyType = spawn.Properties.TryGetValue("EnemyType", out var configuredType)
                ? configuredType
                : string.Empty;
            if (!EnemyCharacter.IsSupported(enemyType) || EnemyCharacter.IsBossType(enemyType))
                continue;

            _enemies.Add(new EnemyInstance(EnemyCharacter.Load(Content, enemyType), spawn));
        }
    }

    private void UpdateAct01(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_player.Animation == PlayerAnimationState.Dead)
        {
            _player.Update(elapsedSeconds);
            if (_player.IsDeadAnimationComplete)
                ResetPlayerAfterDefeat();
            return;
        }

        _playerInvulnerability = Math.Max(0f, _playerInvulnerability - elapsedSeconds);
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
                    StartAction(PlayerAnimationState.Cast);
                else if (ActionButtonBounds(4).Contains(point))
                    StartAction(PlayerAnimationState.Eating);
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
        foreach (var enemy in _enemies)
            enemy.Update(elapsedSeconds, _playerPosition, _level);
        ResolveCombat();
        ResolvePlayerEnemyCollisions();
        if (!_player.IsActionActive)
        {
            var locomotion = !_isGrounded
                ? PlayerAnimationState.Jump
                : (_moveInput.Length() > 0.05f ? PlayerAnimationState.Walk : PlayerAnimationState.Idle);
            _player.SetLocomotion(locomotion);
        }
        UpdateCamera();
    }

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

    private void ResolveCombat()
    {
        if (!_playerAttackHitApplied && _player.IsActionActive &&
            _player.Animation is PlayerAnimationState.Attack1 or PlayerAnimationState.Attack2)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsAlive && PlayerAttackHitbox.Intersects(enemy.Hitbox) &&
                    enemy.TakeDamage(CombatBalance.PlayerAttackDamage, _playerPosition.X))
                {
                    _playerAttackHitApplied = true;
                }
            }
        }

        var playerHitbox = PlayerHitbox;
        foreach (var enemy in _enemies)
        {
            if (enemy.TryConsumeAttackHit(playerHitbox))
                TakePlayerDamage(CombatBalance.EnemyAttackDamage, enemy.Position.X);
        }
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
    }

    private void TakePlayerDamage(int damage, float sourceX)
    {
        if (_playerInvulnerability > 0f || _playerHealth <= 0)
            return;

        _playerHealth = Math.Max(0, _playerHealth - damage);
        _playerInvulnerability = 0.75f;
        _playerKnockbackVelocity = _playerPosition.X < sourceX ? -96f : 96f;
        _verticalVelocity = -110f;
        _isGrounded = false;

        if (_playerHealth == 0)
            _player.SetDead();
    }

    private void ResetPlayerAfterDefeat()
    {
        _playerHealth = CombatBalance.PlayerMaxHealth;
        _playerPosition = _level.PlayerSpawn;
        _verticalVelocity = 0f;
        _playerKnockbackVelocity = 0f;
        _isGrounded = true;
        _jumpsUsed = 0;
        _playerInvulnerability = 1f;
        _player.Reset();
    }

    private void StartAction(PlayerAnimationState action)
    {
        _player.SetAction(action);
        if (action is PlayerAnimationState.Attack1 or PlayerAnimationState.Attack2)
            _playerAttackHitApplied = false;
        _runRemaining = action == PlayerAnimationState.Run ? _player.ActionRemaining : 0f;
    }

    private void TryJump()
    {
        if (_jumpsUsed >= MaximumJumps)
            return;

        _verticalVelocity = JumpVelocity;
        _isGrounded = false;
        _jumpsUsed++;
        StartAction(PlayerAnimationState.Jump);
    }

    private void UpdateCamera()
    {
        var halfWidth = VirtualWidth / 2f;
        var cameraX = Math.Clamp(_playerPosition.X, halfWidth, _level.WorldBounds.Width - halfWidth);
        var halfHeight = VirtualHeight / 2f;
        var cameraY = Math.Clamp(_playerPosition.Y, halfHeight, _level.WorldBounds.Height - halfHeight);
        _camera.LookAt(new Vector2(cameraX, cameraY));
    }

    private void DrawMainMenu()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();

        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
        DrawRect(new Rectangle(12, 12, VirtualWidth - 24, VirtualHeight - 24), new Color(25, 35, 45), false, 2);
        DrawTextCentered(LocalizedText.Get("Title"), new Vector2(VirtualWidth / 2f, 30f), new Color(214, 191, 135), _fontSemiboldWide, 1.25f);
        DrawTextCentered(LocalizedText.Get("MenuSubtitle"), new Vector2(VirtualWidth / 2f, 50f), new Color(117, 143, 145), 0.55f);

        for (var index = 0; index < _menuKeys.Length; index++)
        {
            var selected = index == _menuSelection;
            var bounds = MenuButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get(_menuKeys[index]), bounds.Center.ToVector2(), selected ? Color.White : new Color(188, 193, 188), 0.85f);
        }

        DrawTextCentered($"{LocalizedText.Get("Language")}: {_languageCodes[_languageSelection]}", new Vector2(VirtualWidth / 2f, 165f), new Color(130, 145, 149), 0.58f);
        EndUi();
    }

    private void DrawIntro()
    {
        GraphicsDevice.Clear(new Color(3, 5, 9));
        BeginUi();

        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(3, 5, 9));

        var startY = VirtualHeight - 20f - _introScroll;
        for (var index = 0; index < IntroLines.Length; index++)
        {
            var line = IntroLines[index];
            if (line.Length == 0)
                continue;

            var y = startY + index * IntroLineHeight;
            if (y < -18f || y > VirtualHeight + 18f)
                continue;

            var depth = Math.Clamp((y + 18f) / (VirtualHeight + 36f), 0f, 1f);
            var horizontalScale = 0.48f + depth * 0.55f;
            var verticalScale = 0.62f;
            var color = Color.Lerp(new Color(194, 158, 74), new Color(255, 224, 143), depth);
            DrawTextCentered(
                line,
                new Vector2(VirtualWidth / 2f, y),
                color,
                _fontWide,
                new Vector2(horizontalScale, verticalScale));
        }

        var fadeHeight = 22;
        DrawRect(new Rectangle(0, 0, VirtualWidth, fadeHeight), new Color(3, 5, 9, 235));
        DrawRect(new Rectangle(0, VirtualHeight - fadeHeight, VirtualWidth, fadeHeight), new Color(3, 5, 9, 235));
        EndUi();
    }

    private void DrawOptions()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
        DrawTextCentered(LocalizedText.Get("Options"), new Vector2(VirtualWidth / 2f, 27f), new Color(214, 191, 135), _fontSemibold, 1.2f);
        DrawTextCentered(LocalizedText.Get("Language"), new Vector2(VirtualWidth / 2f, 38f), new Color(156, 170, 169), 0.55f);

        for (var index = 0; index < _languageCodes.Length; index++)
        {
            var selected = index == _optionsSelection;
            var bounds = LanguageButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            var label = _languageCodes[index] switch
            {
                "pt-BR" => LocalizedText.Get("Portuguese"),
                "en-US" => LocalizedText.Get("English"),
                _ => LocalizedText.Get("French")
            };
            DrawTextCentered($"{_languageCodes[index]} - {label}", bounds.Center.ToVector2(), Color.White, 0.55f);
        }

        if (IsDesktop)
        {
            var controls = ControlsButton();
            var selected = _optionsSelection == ControlsOptionIndex;
            DrawRect(controls, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(controls, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            DrawTextCentered(LocalizedText.Get("Controls"), controls.Center.ToVector2(), Color.White, 0.58f);
        }

        DrawAudioOption(BgmVolumeButton(), _optionsSelection == BgmVolumeOptionIndex,
            $"{LocalizedText.Get("BgmVolume")}: {VolumePercent(_audio.BgmVolume)}");
        DrawAudioOption(SfxVolumeButton(), _optionsSelection == SfxVolumeOptionIndex,
            $"{LocalizedText.Get("SfxVolume")}: {VolumePercent(_audio.SfxVolume)}");
        DrawAudioOption(MuteButton(), _optionsSelection == MuteOptionIndex,
            $"{LocalizedText.Get("Mute")}: {(_audio.IsMuted ? LocalizedText.Get("On") : LocalizedText.Get("Off"))}");

        var back = OptionsBackButton();
        var backSelected = _optionsSelection == BackOptionIndex;
        DrawRect(back, backSelected ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(back, backSelected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.62f);
        EndUi();
    }

    private void DrawAudioOption(Rectangle bounds, bool selected, string label)
    {
        DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
        DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
        DrawTextCentered(label, bounds.Center.ToVector2(), Color.White, 0.52f);
    }

    private string VolumePercent(float volume) => $"{(int)Math.Round(volume * 100f)}%";

    private void DrawControls()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
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
        DrawTextCentered(instruction, new Vector2(VirtualWidth / 2f, 145f), new Color(156, 170, 169), 0.45f);

        var back = ControlsBackButton();
        DrawRect(back, new Color(42, 53, 63));
        DrawRect(back, new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.62f);
        EndUi();
    }

    private void DrawPlayerSelection()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
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
        _player.Draw(_spriteBatch, new Vector2(VirtualWidth / 2f, 115f), 0.42f, SpriteEffects.None);
        DrawTextCentered(CharacterLabel(_characterSelection), new Vector2(VirtualWidth / 2f, 133f), new Color(214, 191, 135), 0.65f);

        DrawSelectionButtons();
        EndUi();
    }

    private void DrawDifficultySelection()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
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

        var backButton = SelectionBackButton();
        DrawRect(backButton, new Color(42, 53, 63));
        DrawRect(backButton, new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Back"), backButton.Center.ToVector2(), Color.White, 0.64f);
    }

    private void DrawAct01(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        _level.Draw(gameTime, _spriteBatch, _camera);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: _camera.GetViewMatrix());
        foreach (var enemy in _enemies)
        {
            enemy.Draw(_spriteBatch);
            DrawWorldHealthBar(enemy.Position, enemy.Health, enemy.MaxHealth);
        }
        DrawPlayer();
        _spriteBatch.End();

        BeginUi();
        DrawPlayerHealthBar();
        if (IsMobile)
        {
            DrawAnalog();
            DrawActionButtons();
        }
        EndUi();
    }

    private void DrawPlayer()
    {
        var effects = _playerFacing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _player.Draw(_spriteBatch, _playerPosition, PlayerScale, effects);
    }

    private void DrawPlayerHealthBar()
    {
        DrawText(LocalizedText.Get("Health"), new Vector2(8f, 5f), Color.White, 0.48f);
        DrawHealthBar(new Rectangle(8, 17, 92, 7), _playerHealth, CombatBalance.PlayerMaxHealth);
    }

    private void DrawWorldHealthBar(Vector2 feetPosition, int health, int maximumHealth)
    {
        var bounds = new Rectangle((int)feetPosition.X - 16, (int)feetPosition.Y - 40, 32, 4);
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

    private void BeginUi()
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: _viewportAdapter.GetScaleMatrix());
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
        var size = font.MeasureString(text) * scale;
        _spriteBatch.DrawString(font, text, center, color, 0f, size / 2f, scale, SpriteEffects.None, 0f);
    }

    private void DrawTextCentered(string text, Vector2 center, Color color, SpriteFont font, Vector2 scale)
    {
        var size = font.MeasureString(text);
        _spriteBatch.DrawString(font, text, center, color, 0f, size / 2f, scale, SpriteEffects.None, 0f);
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

    private Rectangle MenuButton(int index) => new(80, 65 + index * 30, 160, 24);

    private Rectangle LanguageButton(int index) => new(66, 43 + index * 16, 188, 14);

    private Rectangle CharacterSelectionButton(int index) => new(8 + index * 104, 49, 96, 22);

    private Rectangle DifficultyButton(int index) => new(72, 50 + index * 24, 176, 20);

    private Rectangle SelectionContinueButton() => new(112, 148, 96, 18);

    private Rectangle SelectionBackButton() => new(8, 148, 72, 18);

    private Rectangle OptionsBackButton() => new(112, 160, 96, 16);

    private Rectangle ControlsButton() => new(98, 92, 124, 14);

    private Rectangle BgmVolumeButton() => new(16, 109, 288, 14);

    private Rectangle SfxVolumeButton() => new(16, 125, 288, 14);

    private Rectangle MuteButton() => new(16, 141, 288, 14);

    private Rectangle ControlsKeyboardButton() => new(8, 27, 148, 14);

    private Rectangle ControlsGamepadButton() => new(164, 27, 148, 14);

    private Rectangle ControlRow(int index) => new(16, 42 + index * 16, 288, 15);

    private Rectangle ControlsBackButton() => new(112, 160, 96, 16);

    private bool IsKeyboardMoveDown(KeyboardState keyboard, int index)
    {
        var fallback = index == 0 ? Keys.Left : Keys.Right;
        return keyboard.IsKeyDown(_keyboardBindings[index]) || keyboard.IsKeyDown(fallback);
    }

    private string GetKeyboardBindingLabel(int index) => _keyboardBindings[index].ToString();

    private string GetGamepadBindingLabel(int index) => _gamepadBindings[index].ToString();

    private string CharacterLabel(int index) => _characterIds[index] switch
    {
        "Kunoichi" => LocalizedText.Get("Kunoichi"),
        "Ninja_Monk" => LocalizedText.Get("Monk"),
        _ => LocalizedText.Get("Peasant")
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
        Act01
    }

    private enum BindingDevice
    {
        Keyboard,
        Gamepad
    }

}
