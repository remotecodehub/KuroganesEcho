#if DEBUG
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace KuroganesEcho.Core;

/// <summary>
/// Debug-only cheat code recognizer and in-game cheat dialog.
/// This type is intentionally compiled out of release builds.
/// </summary>
internal sealed class DebugCheatCodeSystem
{
    private const int VirtualWidth = 320;
    private const int VirtualHeight = 180;
    private const int VisibleRowCount = 8;

    private static readonly Buttons[] DesktopSequence =
    {
        Buttons.LeftTrigger, Buttons.LeftShoulder,
        Buttons.RightTrigger, Buttons.RightShoulder,
        Buttons.DPadUp, Buttons.DPadDown,
        Buttons.DPadUp, Buttons.DPadDown
    };

    private static readonly MobileSequenceButton[] MobileSequence =
    {
        MobileSequenceButton.X, MobileSequenceButton.Y, MobileSequenceButton.Z,
        MobileSequenceButton.A, MobileSequenceButton.B, MobileSequenceButton.C,
        MobileSequenceButton.Up, MobileSequenceButton.Down,
        MobileSequenceButton.Up, MobileSequenceButton.Down
    };

    private readonly List<DebugRow> _rows = new();
    private int _sequenceIndex;
    private int _selectedRow;
    private int _scrollOffset;
    private bool _isOpen;
    private bool _restoreResourcesRequested;
    private CheckpointOption? _selectedCheckpoint;
    private IReadOnlyList<CheckpointOption> _checkpoints = Array.Empty<CheckpointOption>();

    public bool IsOpen => _isOpen;
    public bool InfiniteHealth { get; private set; }
    public bool InfiniteStamina { get; private set; }
    public bool InfinitePower { get; private set; }
    public bool InfiniteLives { get; private set; }

    public enum MobileSequenceButton
    {
        X,
        Y,
        Z,
        A,
        B,
        C,
        Up,
        Down
    }

    public readonly record struct CheckpointOption(int Id, string Name, Vector2 RespawnPosition);

    public bool Update(
        GamePadState currentGamePad,
        GamePadState previousGamePad,
        TouchCollection touches,
        bool isMobile,
        Func<Vector2, Point> toVirtualPoint,
        Func<Point, MobileSequenceButton?> resolveMobileButton,
        IReadOnlyList<CheckpointOption> checkpoints)
    {
        _checkpoints = checkpoints;

        if (_isOpen)
        {
            RefreshRows();
            UpdateDialog(currentGamePad, previousGamePad, touches, toVirtualPoint);
            return true;
        }

        if (isMobile)
        {
            foreach (var touch in touches)
            {
                if (touch.State != TouchLocationState.Pressed)
                    continue;

                var button = resolveMobileButton(toVirtualPoint(touch.Position));
                if (button.HasValue)
                    TrackMobileButton(button.Value);
            }
        }
        else
        {
            foreach (var button in DesktopSequence)
            {
                if (IsNewButtonPress(currentGamePad, previousGamePad, button))
                {
                    TrackDesktopButton(button);
                    break;
                }
            }
        }

        return _isOpen;
    }

    public bool TryConsumeRestoreResources()
    {
        if (!_restoreResourcesRequested)
            return false;

        _restoreResourcesRequested = false;
        return true;
    }

    public bool TryConsumeCheckpoint(out CheckpointOption checkpoint)
    {
        if (!_selectedCheckpoint.HasValue)
        {
            checkpoint = default;
            return false;
        }

        checkpoint = _selectedCheckpoint.Value;
        _selectedCheckpoint = null;
        return true;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D whiteTexture, SpriteFont font, SpriteFont titleFont)
    {
        DrawRect(spriteBatch, whiteTexture, new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(4, 7, 12, 225));

        var dialog = new Rectangle(16, 8, VirtualWidth - 32, VirtualHeight - 16);
        DrawRect(spriteBatch, whiteTexture, dialog, new Color(20, 29, 39, 250));
        DrawRect(spriteBatch, whiteTexture, dialog, new Color(214, 191, 135), false, 2);
        DrawTextCentered(spriteBatch, titleFont, "DEBUG CHEAT CODES", new Vector2(VirtualWidth / 2f, 19f), new Color(214, 191, 135), 0.72f);

        var firstVisibleRow = _scrollOffset;
        var lastVisibleRow = Math.Min(_rows.Count, firstVisibleRow + VisibleRowCount);
        for (var rowIndex = firstVisibleRow; rowIndex < lastVisibleRow; rowIndex++)
        {
            var visibleIndex = rowIndex - firstVisibleRow;
            var bounds = new Rectangle(26, 31 + visibleIndex * 14, 268, 13);
            var selected = rowIndex == _selectedRow;
            DrawRect(spriteBatch, whiteTexture, bounds, selected ? new Color(78, 96, 105) : new Color(42, 53, 63));
            DrawRect(spriteBatch, whiteTexture, bounds, selected ? new Color(214, 191, 135) : new Color(77, 91, 99), false, 1);

            var label = GetRowLabel(_rows[rowIndex]);
            DrawTextCentered(spriteBatch, font, label, bounds.Center.ToVector2(), Color.White, FitTextScale(font, label, bounds.Width - 8, 0.46f));
        }

        if (_scrollOffset > 0)
            DrawTextCentered(spriteBatch, font, "^", new Vector2(302f, 35f), new Color(214, 191, 135), 0.48f);
        if (_scrollOffset + VisibleRowCount < _rows.Count)
            DrawTextCentered(spriteBatch, font, "v", new Vector2(302f, 136f), new Color(214, 191, 135), 0.48f);

        DrawTextCentered(
            spriteBatch,
            font,
            "GAMEPAD: D-PAD / A / B   TOUCH: TAP",
            new Vector2(VirtualWidth / 2f, 159f),
            new Color(156, 170, 169),
            0.35f);
    }

    private void TrackDesktopButton(Buttons button)
    {
        var token = button switch
        {
            Buttons.LeftTrigger => 0,
            Buttons.LeftShoulder => 1,
            Buttons.RightTrigger => 2,
            Buttons.RightShoulder => 3,
            Buttons.DPadUp => 4,
            Buttons.DPadDown => 5,
            _ => -1
        };

        if (token < 0)
            return;

        var expected = DesktopSequence[_sequenceIndex];
        if (button == expected)
            _sequenceIndex++;
        else
            _sequenceIndex = button == DesktopSequence[0] ? 1 : 0;

        if (_sequenceIndex >= DesktopSequence.Length)
            Open();
    }

    private void TrackMobileButton(MobileSequenceButton button)
    {
        if (button == MobileSequence[_sequenceIndex])
            _sequenceIndex++;
        else
            _sequenceIndex = button == MobileSequence[0] ? 1 : 0;

        if (_sequenceIndex >= MobileSequence.Length)
            Open();
    }

    private void Open()
    {
        _sequenceIndex = 0;
        _isOpen = true;
        RefreshRows();
        _selectedRow = 0;
        _scrollOffset = 0;
    }

    private void RefreshRows()
    {
        _rows.Clear();
        _rows.Add(new DebugRow(DebugRowKind.InfiniteHealth));
        _rows.Add(new DebugRow(DebugRowKind.InfiniteStamina));
        _rows.Add(new DebugRow(DebugRowKind.InfinitePower));
        _rows.Add(new DebugRow(DebugRowKind.InfiniteLives));
        _rows.Add(new DebugRow(DebugRowKind.RestoreResources));
        for (var index = 0; index < _checkpoints.Count; index++)
            _rows.Add(new DebugRow(DebugRowKind.Checkpoint, index));
        _rows.Add(new DebugRow(DebugRowKind.Close));

        _selectedRow = Math.Clamp(_selectedRow, 0, Math.Max(0, _rows.Count - 1));
        EnsureSelectedRowVisible();
    }

    private void UpdateDialog(
        GamePadState currentGamePad,
        GamePadState previousGamePad,
        TouchCollection touches,
        Func<Vector2, Point> toVirtualPoint)
    {
        if (IsNewButtonPress(currentGamePad, previousGamePad, Buttons.B) ||
            IsNewButtonPress(currentGamePad, previousGamePad, Buttons.Back))
        {
            _isOpen = false;
            return;
        }

        if (IsNewButtonPress(currentGamePad, previousGamePad, Buttons.DPadUp))
            MoveSelection(-1);
        if (IsNewButtonPress(currentGamePad, previousGamePad, Buttons.DPadDown))
            MoveSelection(1);
        if (IsNewButtonPress(currentGamePad, previousGamePad, Buttons.A))
            ActivateSelectedRow();

        foreach (var touch in touches)
        {
            if (touch.State != TouchLocationState.Pressed)
                continue;

            var point = toVirtualPoint(touch.Position);
            for (var rowIndex = _scrollOffset; rowIndex < Math.Min(_rows.Count, _scrollOffset + VisibleRowCount); rowIndex++)
            {
                var bounds = new Rectangle(26, 31 + (rowIndex - _scrollOffset) * 14, 268, 13);
                if (!bounds.Contains(point))
                    continue;

                _selectedRow = rowIndex;
                ActivateSelectedRow();
                return;
            }
        }
    }

    private void MoveSelection(int direction)
    {
        if (_rows.Count == 0)
            return;

        _selectedRow = (_selectedRow + direction + _rows.Count) % _rows.Count;
        EnsureSelectedRowVisible();
    }

    private void EnsureSelectedRowVisible()
    {
        if (_selectedRow < _scrollOffset)
            _scrollOffset = _selectedRow;
        else if (_selectedRow >= _scrollOffset + VisibleRowCount)
            _scrollOffset = _selectedRow - VisibleRowCount + 1;
    }

    private void ActivateSelectedRow()
    {
        if (_selectedRow < 0 || _selectedRow >= _rows.Count)
            return;

        var row = _rows[_selectedRow];
        switch (row.Kind)
        {
            case DebugRowKind.InfiniteHealth:
                InfiniteHealth = !InfiniteHealth;
                break;
            case DebugRowKind.InfiniteStamina:
                InfiniteStamina = !InfiniteStamina;
                break;
            case DebugRowKind.InfinitePower:
                InfinitePower = !InfinitePower;
                break;
            case DebugRowKind.InfiniteLives:
                InfiniteLives = !InfiniteLives;
                break;
            case DebugRowKind.RestoreResources:
                _restoreResourcesRequested = true;
                break;
            case DebugRowKind.Checkpoint:
                if (row.CheckpointIndex >= 0 && row.CheckpointIndex < _checkpoints.Count)
                {
                    _selectedCheckpoint = _checkpoints[row.CheckpointIndex];
                    _isOpen = false;
                }
                break;
            case DebugRowKind.Close:
                _isOpen = false;
                break;
        }
    }

    private string GetRowLabel(DebugRow row)
    {
        return row.Kind switch
        {
            DebugRowKind.InfiniteHealth => $"Infinite health: {(InfiniteHealth ? "ON" : "OFF")}",
            DebugRowKind.InfiniteStamina => $"Infinite stamina: {(InfiniteStamina ? "ON" : "OFF")}",
            DebugRowKind.InfinitePower => $"Infinite power: {(InfinitePower ? "ON" : "OFF")}",
            DebugRowKind.InfiniteLives => $"Infinite lives: {(InfiniteLives ? "ON" : "OFF")}",
            DebugRowKind.RestoreResources => "Restore all resources",
            DebugRowKind.Checkpoint => GetCheckpointLabel(row.CheckpointIndex),
            _ => "Close"
        };
    }

    private string GetCheckpointLabel(int index)
    {
        if (index < 0 || index >= _checkpoints.Count)
            return "Checkpoint";

        var checkpoint = _checkpoints[index];
        return $"Checkpoint {checkpoint.Id}: {checkpoint.Name}";
    }

    private static float FitTextScale(SpriteFont font, string text, int maxWidth, float preferredScale)
    {
        var width = font.MeasureString(text).X;
        return width <= 0f ? preferredScale : Math.Min(preferredScale, maxWidth / width);
    }

    private static bool IsNewButtonPress(GamePadState current, GamePadState previous, Buttons button) =>
        current.IsButtonDown(button) && !previous.IsButtonDown(button);

    private static void DrawTextCentered(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 center, Color color, float scale)
    {
        var size = font.MeasureString(text) * scale;
        spriteBatch.DrawString(font, text, center - size / 2f, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private static void DrawRect(SpriteBatch spriteBatch, Texture2D whiteTexture, Rectangle bounds, Color color, bool filled = true, int thickness = 1)
    {
        if (filled)
        {
            spriteBatch.Draw(whiteTexture, bounds, color);
            return;
        }

        spriteBatch.Draw(whiteTexture, new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness), color);
        spriteBatch.Draw(whiteTexture, new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness), color);
        spriteBatch.Draw(whiteTexture, new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height), color);
        spriteBatch.Draw(whiteTexture, new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height), color);
    }

    private readonly record struct DebugRow(DebugRowKind Kind, int CheckpointIndex = -1);

    private enum DebugRowKind
    {
        InfiniteHealth,
        InfiniteStamina,
        InfinitePower,
        InfiniteLives,
        RestoreResources,
        Checkpoint,
        Close
    }
}
#endif
