namespace ScrollShot.Scroll;

public static class StitchingProfiles
{
    public const string Current = "current";
    public const string SignalZoneExperiment = "signal-zone";

    public static bool IsKnown(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        return Normalize(profileName) is Current or SignalZoneExperiment;
    }

    public static string Normalize(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        return profileName.Trim().ToLowerInvariant();
    }
}
