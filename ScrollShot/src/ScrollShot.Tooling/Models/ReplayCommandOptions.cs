using ScrollShot.Scroll;

namespace ScrollShot.Tooling.Models;

public sealed class ReplayCommandOptions
{
    public required string ManifestPath { get; init; }

    public required string OutputDirectory { get; init; }

    public string ProfileName { get; init; } = StitchingProfiles.Current;

    public bool PersistOutputImage { get; init; } = true;

    public bool PersistReplayReport { get; init; } = true;
}
