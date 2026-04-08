using ScrollShot.Capture.Models;

namespace ScrollShot.Capture;

public static class ScreenCapturerFactory
{
    public static IScreenCapturer Create(ScreenRect region)
    {
        return Create(region, static () => new DxgiScreenCapturer(), static () => new GdiScreenCapturer());
    }

    public static IScreenCapturer Create(
        ScreenRect region,
        Func<IScreenCapturer> primaryFactory,
        Func<IScreenCapturer> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(primaryFactory);
        ArgumentNullException.ThrowIfNull(fallbackFactory);

        var primary = primaryFactory();
        primary.Initialize(region);

        if (primary.IsAvailable)
        {
            return primary;
        }

        primary.Dispose();

        var fallback = fallbackFactory();
        fallback.Initialize(region);
        return fallback;
    }
}
