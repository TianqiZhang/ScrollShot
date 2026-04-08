namespace ScrollShot.Tooling.Models;

public sealed class SliceCommandOptions
{
    public required string InputImagePath { get; init; }

    public required string OutputDirectory { get; init; }

    public string? DatasetName { get; init; }

    public int? ViewportWidth { get; init; }

    public required int ViewportHeight { get; init; }

    public int CropX { get; init; }

    public int? StepPixels { get; init; }

    public int? OverlapPixels { get; init; }
}
