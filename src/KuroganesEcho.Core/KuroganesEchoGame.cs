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

    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _languageCodes = { "pt-BR", "en-US", "fr-FR" };
    private readonly string[] _menuKeys = { "Play", "Options", "Quit" };
    private readonly string[] _difficultyKeys = { "Easy", "Normal", "Hard" };

    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private Texture2D _whiteTexture = null!;
    private PlayerCharacter _player = null!;
    private GameLevel _level = null!;
    private readonly List<EnemyInstance> _enemies = new();
    private BoxingViewportAdapter _viewportAdapter = null!;
    private OrthographicCamera _camera = null!;
    private readonly string[] _characterIds = { "Kunoichi", "Ninja_Monk", "Ninja_Peasant" };

    private GameScreen _screen = GameScreen.MainMenu;
    private int _menuSelection;
    private int _languageSelection;
    private int _characterSelection;
    private int _difficultySelection = 1;
    private GameDifficulty _selectedDifficulty = GameDifficulty.Normal;
    private Vector2 _playerPosition = new(64f, 352f);
    private Vector2 _moveInput;
    private Vector2 _analogVisualInput;
    private float _verticalVelocity;
    private bool _isGrounded;
    private float _runRemaining;
    private int _jumpsUsed;
    private KeyboardState _previousKeyboard;
    private GamePadState _previousGamePad;
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
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/Hud");
        _level = GameLevel.Load(GraphicsDevice, LevelDefinition.Act01);
        _player = PlayerCharacter.Load(Content, _characterIds[_characterSelection]);
        LoadEnemies();

        _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        _whiteTexture.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);
        var backPressed = IsNewKeyPress(keyboard, Keys.Escape) || gamePad.Buttons.Back == ButtonState.Pressed;
        var touchState = TouchPanel.GetState();

        if (backPressed)
        {
            if (_screen == GameScreen.MainMenu)
                Exit();
            else if (_screen == GameScreen.DifficultySelection)
                _screen = GameScreen.PlayerSelection;
            else if (_screen == GameScreen.PlayerSelection)
                _screen = GameScreen.MainMenu;
            else
                _screen = GameScreen.MainMenu;
        }
        else if (_screen == GameScreen.MainMenu)
        {
            UpdateMainMenu(keyboard, touchState);
        }
        else if (_screen == GameScreen.Options)
        {
            UpdateOptions(keyboard, touchState);
        }
        else if (_screen == GameScreen.PlayerSelection)
        {
            UpdatePlayerSelection(keyboard, touchState);
        }
        else if (_screen == GameScreen.DifficultySelection)
        {
            UpdateDifficultySelection(keyboard, touchState);
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
        else if (_screen == GameScreen.Options)
            DrawOptions();
        else if (_screen == GameScreen.PlayerSelection)
            DrawPlayerSelection();
        else if (_screen == GameScreen.DifficultySelection)
            DrawDifficultySelection();
        else
            DrawMainMenu();

        base.Draw(gameTime);
    }

    private void UpdateMainMenu(KeyboardState keyboard, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Up))
            _menuSelection = (_menuSelection + _menuKeys.Length - 1) % _menuKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Down))
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

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space))
            ActivateMainMenuSelection();
    }

    private void ActivateMainMenuSelection()
    {
        switch (_menuSelection)
        {
            case 0:
                _screen = GameScreen.PlayerSelection;
                break;
            case 1:
                _screen = GameScreen.Options;
                break;
            case 2:
                Exit();
                break;
        }
    }

    private void UpdateOptions(KeyboardState keyboard, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Up))
            _languageSelection = (_languageSelection + _languageCodes.Length - 1) % _languageCodes.Length;
        if (IsNewKeyPress(keyboard, Keys.Down))
            _languageSelection = (_languageSelection + 1) % _languageCodes.Length;
        var point = GetPressedPoint(touches);
        if (point.HasValue)
        {
            for (var index = 0; index < _languageCodes.Length; index++)
            {
                if (LanguageButton(index).Contains(point.Value))
                {
                    SelectLanguage(index);
                    return;
                }
            }

            if (OptionsBackButton().Contains(point.Value))
            {
                _screen = GameScreen.MainMenu;
                return;
            }

        }

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space))
            SelectLanguage(_languageSelection);
    }

    private void UpdatePlayerSelection(KeyboardState keyboard, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Left))
            SelectCharacter(_characterSelection - 1);
        if (IsNewKeyPress(keyboard, Keys.Right))
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

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space))
            OpenDifficultySelection();
    }

    private void OpenDifficultySelection()
    {
        _difficultySelection = (int)_selectedDifficulty;
        _screen = GameScreen.DifficultySelection;
    }

    private void UpdateDifficultySelection(KeyboardState keyboard, TouchCollection touches)
    {
        if (IsNewKeyPress(keyboard, Keys.Up) || IsNewKeyPress(keyboard, Keys.Left))
            _difficultySelection = (_difficultySelection + _difficultyKeys.Length - 1) % _difficultyKeys.Length;
        if (IsNewKeyPress(keyboard, Keys.Down) || IsNewKeyPress(keyboard, Keys.Right))
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

        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Space))
            ConfirmDifficulty();
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
            if (enemyType is not ("Black_Werewolf" or "Kitsune" or "Skeleton_Warrior"))
                continue;

            _enemies.Add(new EnemyInstance(EnemyCharacter.Load(Content, enemyType), spawn));
        }
    }

    private void UpdateAct01(GameTime gameTime, KeyboardState keyboard, GamePadState gamePad, TouchCollection touches)
    {
        _moveInput = Vector2.Zero;
        _analogVisualInput = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            _moveInput.X -= 1f;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            _moveInput.X += 1f;
        if (gamePad.DPad.Left == ButtonState.Pressed)
            _moveInput.X -= 1f;
        if (gamePad.DPad.Right == ButtonState.Pressed)
            _moveInput.X += 1f;
        if (gamePad.ThumbSticks.Left.Length() > 0.2f)
            _moveInput.X = gamePad.ThumbSticks.Left.X;

        if (IsDesktop)
        {
            if (IsNewKeyPress(keyboard, Keys.Z) || IsNewButtonPress(gamePad, Buttons.A))
                StartAction(PlayerAnimationState.Attack1);
            else if (IsNewKeyPress(keyboard, Keys.X) || IsNewButtonPress(gamePad, Buttons.B))
                StartAction(PlayerAnimationState.Attack2);
            else if (IsNewKeyPress(keyboard, Keys.C) || IsNewButtonPress(gamePad, Buttons.X))
                TryJump();
            else if (IsNewKeyPress(keyboard, Keys.R) || IsNewButtonPress(gamePad, Buttons.Y))
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
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var movementSpeed = _runRemaining > 0f ? RunSpeed : PlayerSpeed;
        _playerPosition.X += _moveInput.X * movementSpeed * elapsedSeconds;
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
        foreach (var enemy in _enemies)
            enemy.Update(elapsedSeconds, _playerPosition, _level);
        if (!_player.IsActionActive)
        {
            var locomotion = !_isGrounded
                ? PlayerAnimationState.Jump
                : (_moveInput.Length() > 0.05f ? PlayerAnimationState.Walk : PlayerAnimationState.Idle);
            _player.SetLocomotion(locomotion);
        }
        UpdateCamera();
    }

    private void StartAction(PlayerAnimationState action)
    {
        _player.SetAction(action);
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
        DrawTextCentered(LocalizedText.Get("Title"), new Vector2(VirtualWidth / 2f, 30f), new Color(214, 191, 135), 1.25f);
        DrawTextCentered("16-BIT // ACT 01", new Vector2(VirtualWidth / 2f, 50f), new Color(117, 143, 145), 0.55f);

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

    private void DrawOptions()
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        BeginUi();
        DrawRect(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 29, 39));
        DrawTextCentered(LocalizedText.Get("Options"), new Vector2(VirtualWidth / 2f, 27f), new Color(214, 191, 135), 1.2f);
        DrawTextCentered(LocalizedText.Get("Language"), new Vector2(VirtualWidth / 2f, 42f), new Color(156, 170, 169), 0.65f);

        for (var index = 0; index < _languageCodes.Length; index++)
        {
            var selected = index == _languageSelection;
            var bounds = LanguageButton(index);
            DrawRect(bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);
            var label = _languageCodes[index] switch
            {
                "pt-BR" => LocalizedText.Get("Portuguese"),
                "en-US" => LocalizedText.Get("English"),
                _ => LocalizedText.Get("French")
            };
            DrawTextCentered($"{_languageCodes[index]} - {label}", bounds.Center.ToVector2(), Color.White, 0.62f);
        }

        var back = OptionsBackButton();
        DrawRect(back, new Color(42, 53, 63));
        DrawRect(back, new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.72f);
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
            enemy.Draw(_spriteBatch);
        DrawPlayer();
        _spriteBatch.End();

        BeginUi();
        if (IsMobile)
        {
            DrawAnalog();
            DrawActionButtons();
        }
        EndUi();
    }

    private void DrawPlayer()
    {
        var effects = _moveInput.X < -0.05f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _player.Draw(_spriteBatch, _playerPosition, PlayerScale, effects);
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

    private Rectangle LanguageButton(int index) => new(66, 49 + index * 19, 188, 16);

    private Rectangle CharacterSelectionButton(int index) => new(8 + index * 104, 49, 96, 22);

    private Rectangle DifficultyButton(int index) => new(72, 50 + index * 24, 176, 20);

    private Rectangle SelectionContinueButton() => new(112, 148, 96, 18);

    private Rectangle SelectionBackButton() => new(8, 148, 72, 18);

    private Rectangle OptionsBackButton() => new(112, 153, 96, 18);

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

    private enum GameScreen
    {
        MainMenu,
        Options,
        PlayerSelection,
        DifficultySelection,
        Act01
    }

}
