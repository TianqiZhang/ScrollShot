namespace ScrollShot.StitchingData.Models;

public sealed class StitchDatasetFrame
{
    public int Index { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public int? OffsetPixels { get; init; }

    public int? ExpectedOverlapWithPreviousPixels { get; init; }

    public DateTimeOffset? CapturedAtUtc { get; init; }

    public long? ElapsedMilliseconds { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string? Trace { get; init; }
}
