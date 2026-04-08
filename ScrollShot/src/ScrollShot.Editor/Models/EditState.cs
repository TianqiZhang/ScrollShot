namespace ScrollShot.Editor.Models;

public sealed record EditState
{
    public static EditState Default { get; } = new();

    public TrimRange TrimRange { get; init; } = new(0, 0);

    public IReadOnlyList<CutRange> CutRanges { get; init; } = Array.Empty<CutRange>();

    public CropRect? CropRect { get; init; }

    public bool IncludeChrome { get; init; } = true;
}
