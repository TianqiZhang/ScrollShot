namespace ScrollShot.Scroll;

public static class StitchingProfiles
{
    public const string Current = "current";
    public const string SignalZoneExperiment = "signal-zone";
    public const string SignalHybridExperiment = "signal-hybrid";

    public static bool IsKnown(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        return Normalize(profileName) is Current or SignalZoneExperiment or SignalHybridExperiment;
    }

    public static string Normalize(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        return profileName.Trim().ToLowerInvariant();
    }
}
