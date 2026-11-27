using System;
using System.Collections.Generic;

namespace KuroganesEcho.Core;

internal enum TouchPhase
{
    Invalid,
    Pressed,
    Moved,
    Released
}

internal readonly record struct SlotTouchInput(int Id, TouchPhase State, UiPoint Position);

internal enum SaveSlotAction
{
    None,
    Activate,
    Delete,
    Back
}

internal readonly record struct SaveSlotInputResult(
    SaveSlotAction Action,
    int Slot,
    int? HoldingSlot,
    float HoldProgress);

/// <summary>
/// Independently testable input controller for save slots on both mobile (touch tap &amp; long-press)
/// and desktop (left-click/confirm vs Delete key / right-click).
/// </summary>
internal sealed class SaveSlotInputController
{
    public const float DefaultLongPressDurationSeconds = 0.75f;
    public const float DefaultTapMaxDurationSeconds = 0.35f;
    public const float DefaultMoveTolerancePixels = 10f;

    private readonly float _longPressDurationSeconds;
    private readonly float _tapMaxDurationSeconds;
    private readonly float _moveToleranceSquared;

    private int _trackedTouchId = -1;
    private int _trackedSlot;
    private UiPoint _pressStartPosition;
    private float _holdElapsedSeconds;
    private bool _deleteTriggeredForTouch;
    private bool _cancelledByMovement;

    public SaveSlotInputController(
        float longPressDurationSeconds = DefaultLongPressDurationSeconds,
        float tapMaxDurationSeconds = DefaultTapMaxDurationSeconds,
        float moveTolerancePixels = DefaultMoveTolerancePixels)
    {
        _longPressDurationSeconds = Math.Max(0.1f, longPressDurationSeconds);
        _tapMaxDurationSeconds = Math.Clamp(tapMaxDurationSeconds, 0.05f, _longPressDurationSeconds);
        var tolerance = Math.Max(1f, moveTolerancePixels);
        _moveToleranceSquared = tolerance * tolerance;
    }

    public float LongPressDurationSeconds => _longPressDurationSeconds;

    public int? HoldingSlot => _trackedTouchId >= 0 && !_deleteTriggeredForTouch && !_cancelledByMovement
        ? _trackedSlot
        : null;

    public float HoldProgress => HoldingSlot.HasValue
        ? Math.Clamp(_holdElapsedSeconds / _longPressDurationSeconds, 0f, 1f)
        : 0f;

    public void Reset()
    {
        _trackedTouchId = -1;
        _trackedSlot = 0;
        _pressStartPosition = default;
        _holdElapsedSeconds = 0f;
        _deleteTriggeredForTouch = false;
        _cancelledByMovement = false;
    }

    /// <summary>
    /// Evaluates mobile touch input for save-slot buttons.
    /// - Short taps inside a slot without scrolling trigger <see cref="SaveSlotAction.Activate"/> on release.
    /// - Holding an occupied slot for <see cref="LongPressDurationSeconds"/> triggers <see cref="SaveSlotAction.Delete"/> exactly once.
    /// - Releasing, cancelling, or moving past tolerance before the threshold prevents deletion.
    /// </summary>
    public SaveSlotInputResult UpdateMobileTouch(
        float elapsedSeconds,
        IReadOnlyList<SlotTouchInput> touches,
        Func<int, UiRect> getSlotBounds,
        Func<int, bool> isSlotOccupied)
    {
        var safeDt = Math.Max(0f, elapsedSeconds);

        if (_trackedTouchId < 0)
        {
            foreach (var touch in touches)
            {
                if (touch.State != TouchPhase.Pressed)
                    continue;

                var hitSlot = FindSlotAt(touch.Position, getSlotBounds);
                if (hitSlot <= 0)
                    continue;

                _trackedTouchId = touch.Id;
                _trackedSlot = hitSlot;
                _pressStartPosition = touch.Position;
                _holdElapsedSeconds = 0f;
                _deleteTriggeredForTouch = false;
                _cancelledByMovement = false;

                var occupied = isSlotOccupied(hitSlot);
                return new SaveSlotInputResult(
                    SaveSlotAction.None,
                    0,
                    occupied ? hitSlot : null,
                    0f);
            }

            return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);
        }

        SlotTouchInput? tracked = null;
        foreach (var touch in touches)
        {
            if (touch.Id == _trackedTouchId)
            {
                tracked = touch;
                break;
            }
        }

        // Touch disappeared or was cancelled by the OS: abort safely without triggering anything.
        if (!tracked.HasValue || tracked.Value.State == TouchPhase.Invalid)
        {
            Reset();
            return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);
        }

        var current = tracked.Value;
        var slotBounds = getSlotBounds(_trackedSlot);
        var dx = current.Position.X - _pressStartPosition.X;
        var dy = current.Position.Y - _pressStartPosition.Y;
        var distanceSquared = dx * dx + dy * dy;

        if (!_cancelledByMovement && (distanceSquared > _moveToleranceSquared || !slotBounds.Contains(current.Position)))
            _cancelledByMovement = true;

        if (current.State == TouchPhase.Released)
        {
            var slot = _trackedSlot;
            var shouldActivate = !_deleteTriggeredForTouch &&
                                 !_cancelledByMovement &&
                                 _holdElapsedSeconds < _tapMaxDurationSeconds &&
                                 slotBounds.Contains(current.Position);
            Reset();
            return shouldActivate
                ? new SaveSlotInputResult(SaveSlotAction.Activate, slot, null, 0f)
                : new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);
        }

        if (_deleteTriggeredForTouch || _cancelledByMovement)
            return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);

        _holdElapsedSeconds += safeDt;

        var isOccupied = isSlotOccupied(_trackedSlot);
        if (!isOccupied)
            return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);

        if (_holdElapsedSeconds >= _longPressDurationSeconds)
        {
            _deleteTriggeredForTouch = true;
            var deletedSlot = _trackedSlot;
            return new SaveSlotInputResult(SaveSlotAction.Delete, deletedSlot, null, 0f);
        }

        return new SaveSlotInputResult(
            SaveSlotAction.None,
            0,
            _trackedSlot,
            Math.Clamp(_holdElapsedSeconds / _longPressDurationSeconds, 0f, 1f));
    }

    /// <summary>
    /// Evaluates desktop save-slot actions. Secondary deletion inputs (Delete key or right mouse button)
    /// take precedence and never trigger the save/load activation action.
    /// </summary>
    public static SaveSlotInputResult EvaluateDesktopInput(
        int? selectedSlot,
        bool deleteKeyPressed,
        bool confirmPressed,
        UiPoint? rightClickPoint,
        UiPoint? leftClickPoint,
        Func<int, UiRect> getSlotBounds)
    {
        if (rightClickPoint.HasValue)
        {
            var rightHitSlot = FindSlotAt(rightClickPoint.Value, getSlotBounds);
            if (rightHitSlot > 0)
                return new SaveSlotInputResult(SaveSlotAction.Delete, rightHitSlot, null, 0f);
        }

        if (deleteKeyPressed && selectedSlot.HasValue && SaveProgressStore.IsValidSlot(selectedSlot.Value))
            return new SaveSlotInputResult(SaveSlotAction.Delete, selectedSlot.Value, null, 0f);

        if (leftClickPoint.HasValue)
        {
            var leftHitSlot = FindSlotAt(leftClickPoint.Value, getSlotBounds);
            if (leftHitSlot > 0)
                return new SaveSlotInputResult(SaveSlotAction.Activate, leftHitSlot, null, 0f);
        }

        if (confirmPressed && selectedSlot.HasValue && SaveProgressStore.IsValidSlot(selectedSlot.Value))
            return new SaveSlotInputResult(SaveSlotAction.Activate, selectedSlot.Value, null, 0f);

        return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);
    }

    /// <summary>
    /// Evaluates gamepad B button (Cancel/Back) input for save-slot screens.
    /// Deletes the selected slot only when it is occupied; empty slots never trigger deletion.
    /// When no occupied slot is selected and <paramref name="allowBackWhenNotDeleting"/> is true,
    /// preserves the back navigation action.
    /// </summary>
    public static SaveSlotInputResult EvaluateGamepadCancelInput(
        int? selectedSlot,
        bool gamepadBPressed,
        Func<int, bool> isSlotOccupied,
        bool allowBackWhenNotDeleting)
    {
        if (!gamepadBPressed)
            return new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);

        if (selectedSlot.HasValue &&
            SaveProgressStore.IsValidSlot(selectedSlot.Value) &&
            isSlotOccupied is not null &&
            isSlotOccupied(selectedSlot.Value))
        {
            return new SaveSlotInputResult(SaveSlotAction.Delete, selectedSlot.Value, null, 0f);
        }

        return allowBackWhenNotDeleting
            ? new SaveSlotInputResult(SaveSlotAction.Back, 0, null, 0f)
            : new SaveSlotInputResult(SaveSlotAction.None, 0, null, 0f);
    }

    private static int FindSlotAt(UiPoint point, Func<int, UiRect> getSlotBounds)
    {
        for (var slot = 1; slot <= SaveProgressStore.SlotCount; slot++)
        {
            if (getSlotBounds(slot).Contains(point))
                return slot;
        }

        return 0;
    }
}
