using System;
using System.IO;
using KuroganesEcho.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using MonoGame.Extended;
using MonoGame.Extended.Content;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended.Tilemaps.Tiled;
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
    private const float PlayerScale = 0.5f;
    private const float Gravity = 480f;
    private const float JumpVelocity = -180f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _languageCodes = { "pt-BR", "en-US", "fr-FR" };
    private readonly string[] _menuKeys = { "Play", "Options", "Quit" };

    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private Texture2D _whiteTexture = null!;
    private Texture2D _playerIdle = null!;
    private Texture2D _playerRun = null!;
    private Texture2D _playerAttack1 = null!;
    private Texture2D _playerAttack2 = null!;
    private Texture2D _playerCast = null!;
    private Texture2D _playerWalk = null!;
    private Texture2D _playerEating = null!;
    private Texture2D _playerJump = null!;
    private Tilemap _tilemap = null!;
    private TilemapSpriteBatchRenderer _tilemapRenderer = null!;
    private BoxingViewportAdapter _viewportAdapter = null!;
    private OrthographicCamera _camera = null!;

    private GameScreen _screen = GameScreen.MainMenu;
    private int _menuSelection;
    private int _languageSelection;
    private Vector2 _playerPosition = new(64f, 352f);
    private Vector2 _moveInput;
    private Vector2 _analogVisualInput;
    private float _verticalVelocity;
    private bool _isGrounded;
    private float _animationClock;
    private float _actionRemaining;
    private PlayerAnimationState _playerAnimation = PlayerAnimationState.Idle;
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
        using (var mapStream = TitleContainer.OpenStream("Content/Tiled/Maps/Act_01.tmx"))
        {
            var parser = new TiledTmxParser(
                "Content/Tiled/Maps",
                OpenTiledResource);
            _tilemap = parser.ParseFromStream(mapStream, GraphicsDevice, "Content/Tiled/Maps");
        }
        _tilemapRenderer = new TilemapSpriteBatchRenderer
        {
            BlendState = BlendState.AlphaBlend,
            SamplerState = SamplerState.PointClamp
        };
        _tilemapRenderer.LoadTilemap(_tilemap);

        _playerIdle = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Idle");
        _playerRun = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Run");
        _playerAttack1 = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Attack_1");
        _playerAttack2 = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Attack_2");
        _playerCast = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Cast");
        _playerWalk = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Walk");
        _playerEating = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Eating");
        _playerJump = Content.Load<Texture2D>("Tiled/Chars/craftpix-net-407836-free-ninja-sprite-sheets-pixel-art/Kunoichi/Jump");

        _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        _whiteTexture.SetData(new[] { Color.White });
    }

    private static Stream OpenTiledResource(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
            normalized = "Content/" + normalized.TrimStart('/');

        return TitleContainer.OpenStream(normalized);
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
                StartAct01();
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

    private void SelectLanguage(int index)
    {
        _languageSelection = Math.Clamp(index, 0, _languageCodes.Length - 1);
        LocalizationManager.SetCulture(_languageCodes[_languageSelection]);
    }

    private void StartAct01()
    {
        _screen = GameScreen.Act01;
        _playerPosition = new Vector2(64f, 352f);
        _moveInput = Vector2.Zero;
        _analogVisualInput = Vector2.Zero;
        _verticalVelocity = 0f;
        _isGrounded = true;
        UpdateCamera();
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
        _playerPosition.X += _moveInput.X * PlayerSpeed * elapsedSeconds;
        _verticalVelocity += Gravity * elapsedSeconds;
        _playerPosition.Y += _verticalVelocity * elapsedSeconds;
        _playerPosition.X = Math.Clamp(_playerPosition.X, 32f, _tilemap.WorldBounds.Width - 32f);
        var groundY = _tilemap.WorldBounds.Height - 32f;
        if (_playerPosition.Y >= groundY)
        {
            _playerPosition.Y = groundY;
            _verticalVelocity = 0f;
            _isGrounded = true;
        }
        else
        {
            _isGrounded = false;
            _playerPosition.Y = Math.Max(32f, _playerPosition.Y);
        }
        _animationClock += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_actionRemaining > 0f)
        {
            _actionRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_actionRemaining <= 0f)
                _playerAnimation = _isGrounded
                    ? (_moveInput.Length() > 0.05f ? PlayerAnimationState.Walk : PlayerAnimationState.Idle)
                    : PlayerAnimationState.Jump;
        }
        else if (!_isGrounded)
            _playerAnimation = PlayerAnimationState.Jump;
        else
        {
            _playerAnimation = _moveInput.Length() > 0.05f ? PlayerAnimationState.Walk : PlayerAnimationState.Idle;
        }
        UpdateCamera();
    }

    private void StartAction(PlayerAnimationState action)
    {
        _playerAnimation = action;
        _actionRemaining = action == PlayerAnimationState.Jump ? 0.9f : 0.65f;
        _animationClock = 0f;
    }

    private void TryJump()
    {
        if (!_isGrounded)
            return;

        _verticalVelocity = JumpVelocity;
        _isGrounded = false;
        StartAction(PlayerAnimationState.Jump);
    }

    private void UpdateCamera()
    {
        var halfWidth = VirtualWidth / 2f;
        var cameraX = Math.Clamp(_playerPosition.X, halfWidth, _tilemap.WorldBounds.Width - halfWidth);
        var halfHeight = VirtualHeight / 2f;
        var cameraY = Math.Clamp(_playerPosition.Y, halfHeight, _tilemap.WorldBounds.Height - halfHeight);
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
        DrawTextCentered(LocalizedText.Get("Language"), new Vector2(VirtualWidth / 2f, 48f), new Color(156, 170, 169), 0.75f);

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
            DrawTextCentered($"{_languageCodes[index]} - {label}", bounds.Center.ToVector2(), Color.White, 0.72f);
        }

        var back = OptionsBackButton();
        DrawRect(back, new Color(42, 53, 63));
        DrawRect(back, new Color(77, 91, 99), false, 1);
        DrawTextCentered(LocalizedText.Get("Back"), back.Center.ToVector2(), Color.White, 0.72f);
        EndUi();
    }

    private void DrawAct01(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 16, 24));
        _tilemapRenderer.Update(gameTime);
        _tilemapRenderer.Draw(_spriteBatch, _camera);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: _camera.GetViewMatrix());
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
        var texture = _playerAnimation switch
        {
            PlayerAnimationState.Attack1 => _playerAttack1,
            PlayerAnimationState.Attack2 => _playerAttack2,
            PlayerAnimationState.Cast => _playerCast,
            PlayerAnimationState.Walk => _playerWalk,
            PlayerAnimationState.Eating => _playerEating,
            PlayerAnimationState.Jump => _playerJump,
            PlayerAnimationState.Run => _playerRun,
            _ => _playerIdle
        };
        var frameCount = _playerAnimation switch
        {
            PlayerAnimationState.Attack1 => 6,
            PlayerAnimationState.Attack2 => 8,
            PlayerAnimationState.Cast => 6,
            PlayerAnimationState.Walk => 8,
            PlayerAnimationState.Eating => 9,
            PlayerAnimationState.Jump => 10,
            PlayerAnimationState.Run => 8,
            _ => 9
        };
        var frameWidth = texture.Width / frameCount;
        var frame = (int)(_animationClock * (_playerAnimation is PlayerAnimationState.Idle ? 7f : 10f)) % frameCount;
        var source = new Rectangle(frame * frameWidth, 0, frameWidth, texture.Height);
        var origin = new Vector2(frameWidth / 2f, texture.Height);
        var effects = _moveInput.X < -0.05f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _spriteBatch.Draw(texture, _playerPosition, source, Color.White, 0f, origin, PlayerScale, effects, 0f);
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

    private Rectangle LanguageButton(int index) => new(66, 61 + index * 25, 188, 20);

    private Rectangle OptionsBackButton() => new(112, 145, 96, 20);

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
        Act01
    }

    private enum PlayerAnimationState
    {
        Idle,
        Run,
        Attack1,
        Attack2,
        Cast,
        Walk,
        Eating,
        Jump
    }
}
