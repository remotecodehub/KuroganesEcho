using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KuroganesEcho.Core.Localization;

/// <summary>
/// Manages cyclic horizontal selection across the cultures supported by the game's
/// localization resources.
/// </summary>
internal sealed class LanguageSelector
{
    private readonly List<CultureInfo> _cultures;

    public LanguageSelector(IEnumerable<CultureInfo> supportedCultures = null, string initialCultureCode = null)
    {
        var list = (supportedCultures ?? LocalizationManager.GetSupportedCultures())
            .Where(culture => culture is not null && !string.IsNullOrWhiteSpace(culture.Name))
            .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (list.Count == 0)
            list.Add(CultureInfo.GetCultureInfo(LocalizationManager.DEFAULT_CULTURE_CODE));

        _cultures = list;
        SelectedIndex = 0;

        if (!string.IsNullOrWhiteSpace(initialCultureCode) && TrySelect(initialCultureCode))
            return;

        TrySelect(LocalizationManager.DEFAULT_CULTURE_CODE);
    }

    public IReadOnlyList<CultureInfo> SupportedCultures => _cultures;

    public int SelectedIndex { get; private set; }

    public CultureInfo SelectedCulture => _cultures[SelectedIndex];

    public string SelectedCode => SelectedCulture.Name;

    public CultureInfo MoveNext() => Move(1);

    public CultureInfo MovePrevious() => Move(-1);

    public CultureInfo Move(int delta)
    {
        var count = _cultures.Count;
        if (count == 0)
            return SelectedCulture;

        SelectedIndex = ((SelectedIndex + delta) % count + count) % count;
        return SelectedCulture;
    }

    public bool TrySelect(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return false;

        var trimmed = cultureCode.Trim();
        for (var index = 0; index < _cultures.Count; index++)
        {
            if (string.Equals(_cultures[index].Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                SelectedIndex = index;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Recognizes intentional horizontal swipes on touch screens within a target region,
/// distinguishing them from taps and vertical scroll gestures.
/// </summary>
internal sealed class HorizontalSwipeDetector
{
    public const float DefaultSwipeThresholdPixels = 16f;

    private readonly float _swipeThreshold;
    private int _activeTouchId = -1;
    private UiPoint _startPoint;
    private UiPoint _lastPoint;
    private bool _swipeTriggered;

    public HorizontalSwipeDetector(float swipeThresholdPixels = DefaultSwipeThresholdPixels)
    {
        _swipeThreshold = Math.Max(4f, swipeThresholdPixels);
    }

    public bool IsTracking => _activeTouchId >= 0;

    public void Reset()
    {
        _activeTouchId = -1;
        _startPoint = default;
        _lastPoint = default;
        _swipeTriggered = false;
    }

    /// <summary>
    /// Updates gesture state with virtual-space touch samples.
    /// Returns +1 for swipe left (advance next), -1 for swipe right (previous), or 0 for none.
    /// Sets <paramref name="consumedTouch"/> to true when a touch belonged to a tracked swipe region
    /// and sets <paramref name="tapReleasedPoint"/> when a tracked touch released cleanly without swiping.
    /// </summary>
    public int Update(
        IReadOnlyList<SlotTouchInput> touches,
        UiRect targetBounds,
        out bool consumedTouch,
        out UiPoint? tapReleasedPoint)
    {
        consumedTouch = false;
        tapReleasedPoint = null;

        if (_activeTouchId < 0)
        {
            foreach (var touch in touches)
            {
                if (touch.State == TouchPhase.Pressed && targetBounds.Contains(touch.Position))
                {
                    _activeTouchId = touch.Id;
                    _startPoint = touch.Position;
                    _lastPoint = touch.Position;
                    _swipeTriggered = false;
                    consumedTouch = true;
                    break;
                }
            }

            return 0;
        }

        SlotTouchInput? tracked = null;
        foreach (var touch in touches)
        {
            if (touch.Id == _activeTouchId)
            {
                tracked = touch;
                break;
            }
        }

        if (!tracked.HasValue || tracked.Value.State == TouchPhase.Invalid)
        {
            Reset();
            consumedTouch = true;
            return 0;
        }

        var current = tracked.Value;
        _lastPoint = current.Position;
        var dx = _lastPoint.X - _startPoint.X;
        var dy = _lastPoint.Y - _startPoint.Y;

        if (current.State == TouchPhase.Moved)
        {
            consumedTouch = true;
            if (!_swipeTriggered && Math.Abs(dx) >= _swipeThreshold && Math.Abs(dx) > Math.Abs(dy))
            {
                _swipeTriggered = true;
                // Swiping left (negative dx) moves forward to the next item; swiping right moves to the previous.
                return dx < 0 ? 1 : -1;
            }

            return 0;
        }

        if (current.State == TouchPhase.Released)
        {
            var direction = 0;
            if (!_swipeTriggered && Math.Abs(dx) >= _swipeThreshold && Math.Abs(dx) > Math.Abs(dy))
            {
                _swipeTriggered = true;
                direction = dx < 0 ? 1 : -1;
            }

            consumedTouch = true;
            if (!_swipeTriggered && targetBounds.Contains(current.Position))
                tapReleasedPoint = current.Position;

            Reset();
            return direction;
        }

        return 0;
    }
}
