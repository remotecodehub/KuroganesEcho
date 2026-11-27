using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using KuroganesEcho.Core;
using KuroganesEcho.Core.Localization;
using Xunit;

namespace KuroganesEcho.Core.Tests;

[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationCollectionDefinition
{
}

[Collection("Localization")]
public sealed class LocalizationTests
{
    [Fact]
    public void SupportedCultures_IncludesAllFourRequiredLanguagesFromResources()
    {
        var cultures = LocalizationManager.GetSupportedCultures();
        var codes = cultures.Select(c => c.Name).ToList();

        Assert.Contains("pt-BR", codes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("en-US", codes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("fr-FR", codes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("es-ES", codes, StringComparer.OrdinalIgnoreCase);
        Assert.True(cultures.Count >= 4);
    }

    [Fact]
    public void EverySupportedLanguageIsSelectableAndAppliesLocalizedResources()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var selector = new LanguageSelector();
            foreach (var culture in selector.SupportedCultures)
            {
                Assert.True(selector.TrySelect(culture.Name));
                Assert.Equal(culture.Name, selector.SelectedCode);
                Assert.True(LocalizationManager.TrySetCulture(culture.Name));
                Assert.Equal(culture.Name, CultureInfo.CurrentUICulture.Name);

                var optionsText = LocalizedText.Get("Options");
                Assert.False(string.IsNullOrWhiteSpace(optionsText));
                Assert.NotEqual("Options_Missing", optionsText);

                var deletedText = LocalizedText.Get("SlotDeleted");
                Assert.Contains("{0}", deletedText);
            }
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
            Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void LeftRightNavigation_WorksAndWrapsAroundBothEnds()
    {
        var selector = new LanguageSelector(initialCultureCode: "pt-BR");
        var count = selector.SupportedCultures.Count;
        Assert.True(count >= 4);
        Assert.Equal(0, selector.SelectedIndex);

        // Move left from first item wraps to last item
        var wrappedLeft = selector.MovePrevious();
        Assert.Equal(count - 1, selector.SelectedIndex);
        Assert.Equal(selector.SupportedCultures[count - 1].Name, wrappedLeft.Name);

        // Move right from last item wraps back to first item
        var wrappedRight = selector.MoveNext();
        Assert.Equal(0, selector.SelectedIndex);
        Assert.Equal(selector.SupportedCultures[0].Name, wrappedRight.Name);

        // Stepping right through all items visits every culture and returns to start
        var visited = new string[count];
        for (var i = 0; i < count; i++)
        {
            visited[i] = selector.SelectedCode;
            selector.MoveNext();
        }

        Assert.Equal(count, visited.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(0, selector.SelectedIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("not-a-culture")]
    public void UnsupportedLanguagesAreRejectedSafely(string invalidCode)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            Assert.True(LocalizationManager.TrySetCulture("en-US"));
            var selector = new LanguageSelector(initialCultureCode: "en-US");

            Assert.False(LocalizationManager.IsSupportedCulture(invalidCode));
            Assert.False(LocalizationManager.TrySetCulture(invalidCode));
            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);

            Assert.False(selector.TrySelect(invalidCode));
            Assert.Equal("en-US", selector.SelectedCode);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
            Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void HorizontalSwipeDetector_DetectsLeftAndRightSwipesAndIgnoresVerticalScroll()
    {
        var bounds = new UiRect(38, 46, 244, 22);
        var detector = new HorizontalSwipeDetector(swipeThresholdPixels: 16f);

        // Swipe left -> returns +1 (next language)
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(1, TouchPhase.Pressed, new UiPoint(160, 56)) }, bounds, out _, out _));
        Assert.Equal(1, detector.Update(new[] { new SlotTouchInput(1, TouchPhase.Moved, new UiPoint(130, 57)) }, bounds, out var consumedLeft, out _));
        Assert.True(consumedLeft);
        // Releasing after swipe does not produce a tap
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(1, TouchPhase.Released, new UiPoint(128, 57)) }, bounds, out _, out var tapAfterSwipe));
        Assert.Null(tapAfterSwipe);

        // Swipe right -> returns -1 (previous language)
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(2, TouchPhase.Pressed, new UiPoint(120, 56)) }, bounds, out _, out _));
        Assert.Equal(-1, detector.Update(new[] { new SlotTouchInput(2, TouchPhase.Moved, new UiPoint(150, 56)) }, bounds, out _, out _));
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(2, TouchPhase.Released, new UiPoint(152, 56)) }, bounds, out _, out _));

        // Vertical drag -> does not trigger horizontal swipe
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(3, TouchPhase.Pressed, new UiPoint(140, 56)) }, bounds, out _, out _));
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(3, TouchPhase.Moved, new UiPoint(144, 90)) }, bounds, out _, out _));
        Assert.Equal(0, detector.Update(new[] { new SlotTouchInput(3, TouchPhase.Released, new UiPoint(144, 92)) }, bounds, out _, out _));
    }
}
