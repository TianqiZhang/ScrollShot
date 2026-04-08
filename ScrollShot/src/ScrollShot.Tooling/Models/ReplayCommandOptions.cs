namespace ScrollShot.Tooling.Models;

public sealed class ReplayCommandOptions
{
    public required string ManifestPath { get; init; }

    public required string OutputDirectory { get; init; }
}
