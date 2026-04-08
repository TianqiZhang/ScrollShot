namespace ScrollShot.Tooling.Models;

public sealed class SyntheticCommandOptions
{
    public required string OutputDirectory { get; init; }

    public string? DatasetName { get; init; }

    public int Width { get; init; } = 480;

    public int TotalHeight { get; init; } = 2200;

    public int ViewportHeight { get; init; } = 420;

    public int? StepPixels { get; init; }

    public int? OverlapPixels { get; init; }

    public int FixedTop { get; init; } = 48;

    public int FixedBottom { get; init; } = 32;
}
