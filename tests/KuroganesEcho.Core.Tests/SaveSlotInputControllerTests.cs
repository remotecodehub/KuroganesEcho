using System;
using KuroganesEcho.Core;
using Xunit;

namespace KuroganesEcho.Core.Tests;

public sealed class SaveSlotInputControllerTests
{
    private static UiRect SlotBounds(int slot) => new(42, 55 + (slot - 1) * 22, 236, 18);

    [Fact]
    public void MobileLongPress_TriggersExactlyOneDeletion_AndReleaseDoesNotActivate()
    {
        var controller = new SaveSlotInputController(longPressDurationSeconds: 0.75f);
        var center = SlotBounds(2).Center;
        Func<int, bool> occupied = slot => slot == 2;

        // Touch down
        var r1 = controller.UpdateMobileTouch(
            0.016f,
            new[] { new SlotTouchInput(1, TouchPhase.Pressed, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, r1.Action);
        Assert.Equal(2, r1.HoldingSlot);

        // Hold below threshold (0.40s)
        var r2 = controller.UpdateMobileTouch(
            0.40f,
            new[] { new SlotTouchInput(1, TouchPhase.Moved, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, r2.Action);
        Assert.Equal(2, r2.HoldingSlot);
        Assert.True(r2.HoldProgress > 0.5f && r2.HoldProgress < 1f);

        // Cross threshold (+0.40s -> 0.80s >= 0.75s): triggers Delete once
        var r3 = controller.UpdateMobileTouch(
            0.40f,
            new[] { new SlotTouchInput(1, TouchPhase.Moved, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.Delete, r3.Action);
        Assert.Equal(2, r3.Slot);

        // Continued holding must NOT trigger a second deletion
        var r4 = controller.UpdateMobileTouch(
            1.00f,
            new[] { new SlotTouchInput(1, TouchPhase.Moved, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, r4.Action);

        // Releasing after deletion must NOT trigger save/load activation
        var r5 = controller.UpdateMobileTouch(
            0.016f,
            new[] { new SlotTouchInput(1, TouchPhase.Released, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, r5.Action);
    }

    [Fact]
    public void MobileShortTap_ActivatesSlotAndDoesNotDelete()
    {
        var controller = new SaveSlotInputController(longPressDurationSeconds: 0.75f);
        var center = SlotBounds(1).Center;
        Func<int, bool> occupied = _ => true;

        var pressed = controller.UpdateMobileTouch(
            0.016f,
            new[] { new SlotTouchInput(10, TouchPhase.Pressed, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, pressed.Action);

        var heldBriefly = controller.UpdateMobileTouch(
            0.10f,
            new[] { new SlotTouchInput(10, TouchPhase.Moved, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, heldBriefly.Action);

        var released = controller.UpdateMobileTouch(
            0.016f,
            new[] { new SlotTouchInput(10, TouchPhase.Released, center) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.Activate, released.Action);
        Assert.Equal(1, released.Slot);
    }

    [Fact]
    public void MobileReleaseBeforeThreshold_OrTouchCancel_OrScrollMovement_PreventsDeletion()
    {
        var controller = new SaveSlotInputController(longPressDurationSeconds: 0.75f, moveTolerancePixels: 10f);
        var center = SlotBounds(3).Center;
        Func<int, bool> occupied = _ => true;

        // 1. Release after 0.60s (< 0.75s): no deletion and no accidental activation
        controller.UpdateMobileTouch(0.016f, new[] { new SlotTouchInput(1, TouchPhase.Pressed, center) }, SlotBounds, occupied);
        var midHold = controller.UpdateMobileTouch(0.60f, new[] { new SlotTouchInput(1, TouchPhase.Moved, center) }, SlotBounds, occupied);
        Assert.Equal(SaveSlotAction.None, midHold.Action);
        var earlyRelease = controller.UpdateMobileTouch(0.016f, new[] { new SlotTouchInput(1, TouchPhase.Released, center) }, SlotBounds, occupied);
        Assert.Equal(SaveSlotAction.None, earlyRelease.Action);

        // 2. Touch cancellation (Invalid state / disappears) before threshold: no deletion
        controller.UpdateMobileTouch(0.016f, new[] { new SlotTouchInput(2, TouchPhase.Pressed, center) }, SlotBounds, occupied);
        controller.UpdateMobileTouch(0.50f, new[] { new SlotTouchInput(2, TouchPhase.Moved, center) }, SlotBounds, occupied);
        var cancelled = controller.UpdateMobileTouch(0.50f, new[] { new SlotTouchInput(2, TouchPhase.Invalid, center) }, SlotBounds, occupied);
        Assert.Equal(SaveSlotAction.None, cancelled.Action);
        Assert.Null(cancelled.HoldingSlot);

        // 3. Movement / scrolling past tolerance cancels long-press and prevents deletion or tap
        controller.UpdateMobileTouch(0.016f, new[] { new SlotTouchInput(3, TouchPhase.Pressed, center) }, SlotBounds, occupied);
        var scrolled = controller.UpdateMobileTouch(
            0.90f,
            new[] { new SlotTouchInput(3, TouchPhase.Moved, new UiPoint(center.X, center.Y + 25)) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, scrolled.Action);
        var releasedAfterScroll = controller.UpdateMobileTouch(
            0.016f,
            new[] { new SlotTouchInput(3, TouchPhase.Released, new UiPoint(center.X, center.Y + 25)) },
            SlotBounds,
            occupied);
        Assert.Equal(SaveSlotAction.None, releasedAfterScroll.Action);
    }

    [Fact]
    public void DesktopDeletionInput_TriggersDeleteAndNeverTriggersSaveAction()
    {
        // Delete key with slot selected (even if confirm pressed simultaneously) -> Delete, not Activate
        var deleteKeyResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot: 2,
            deleteKeyPressed: true,
            confirmPressed: true,
            rightClickPoint: null,
            leftClickPoint: null,
            getSlotBounds: SlotBounds);
        Assert.Equal(SaveSlotAction.Delete, deleteKeyResult.Action);
        Assert.Equal(2, deleteKeyResult.Slot);

        // Right mouse click on slot 1 -> Delete, not Activate
        var rightClickResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot: 3,
            deleteKeyPressed: false,
            confirmPressed: false,
            rightClickPoint: SlotBounds(1).Center,
            leftClickPoint: SlotBounds(1).Center,
            getSlotBounds: SlotBounds);
        Assert.Equal(SaveSlotAction.Delete, rightClickResult.Action);
        Assert.Equal(1, rightClickResult.Slot);

        // Normal left-click still activates slot unchanged
        var leftClickResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot: 1,
            deleteKeyPressed: false,
            confirmPressed: false,
            rightClickPoint: null,
            leftClickPoint: SlotBounds(3).Center,
            getSlotBounds: SlotBounds);
        Assert.Equal(SaveSlotAction.Activate, leftClickResult.Action);
        Assert.Equal(3, leftClickResult.Slot);

        // Normal confirm key still activates selected slot unchanged
        var confirmResult = SaveSlotInputController.EvaluateDesktopInput(
            selectedSlot: 2,
            deleteKeyPressed: false,
            confirmPressed: true,
            rightClickPoint: null,
            leftClickPoint: null,
            getSlotBounds: SlotBounds);
        Assert.Equal(SaveSlotAction.Activate, confirmResult.Action);
        Assert.Equal(2, confirmResult.Slot);
    }

    [Fact]
    public void GamepadButtonB_OnOccupiedSlot_TriggersDeletionInPauseAndSelectionScreens()
    {
        Func<int, bool> occupied = slot => slot == 2;

        // Pause menu: B on occupied slot triggers Delete
        var pauseResult = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: 2,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: false);
        Assert.Equal(SaveSlotAction.Delete, pauseResult.Action);
        Assert.Equal(2, pauseResult.Slot);

        // Save slot selection screen: B on occupied slot triggers Delete instead of Back
        var selectionResult = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: 2,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: true);
        Assert.Equal(SaveSlotAction.Delete, selectionResult.Action);
        Assert.Equal(2, selectionResult.Slot);
    }

    [Fact]
    public void GamepadButtonB_OnEmptySlot_DoesNotTriggerDeletion()
    {
        Func<int, bool> occupied = slot => slot == 1;

        // Pause menu: B on empty slot does nothing (does not delete)
        var pauseEmpty = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: 2,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: false);
        Assert.Equal(SaveSlotAction.None, pauseEmpty.Action);
        Assert.Equal(0, pauseEmpty.Slot);

        // Save slot selection screen: B on empty slot does NOT delete and preserves Back behavior
        var selectionEmpty = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: 2,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: true);
        Assert.Equal(SaveSlotAction.Back, selectionEmpty.Action);
        Assert.Equal(0, selectionEmpty.Slot);
    }

    [Fact]
    public void GamepadButtonB_WhenNoSlotSelected_PreservesBackBehaviorOnSelectionScreenAndNoOpOnPause()
    {
        Func<int, bool> occupied = _ => true;

        // Selection screen with Back option selected (selectedSlot == null): preserves Back
        var selectionBack = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: null,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: true);
        Assert.Equal(SaveSlotAction.Back, selectionBack.Action);
        Assert.Equal(0, selectionBack.Slot);

        // Pause screen with non-slot option selected (selectedSlot == null): does not delete or exit
        var pauseNonSlot = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: null,
            gamepadBPressed: true,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: false);
        Assert.Equal(SaveSlotAction.None, pauseNonSlot.Action);
        Assert.Equal(0, pauseNonSlot.Slot);

        // When B is not pressed: returns None regardless of selected slot
        var notPressed = SaveSlotInputController.EvaluateGamepadCancelInput(
            selectedSlot: 1,
            gamepadBPressed: false,
            isSlotOccupied: occupied,
            allowBackWhenNotDeleting: true);
        Assert.Equal(SaveSlotAction.None, notPressed.Action);
    }
}
