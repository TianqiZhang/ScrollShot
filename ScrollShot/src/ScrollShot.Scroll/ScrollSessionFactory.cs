using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Experiments;

namespace ScrollShot.Scroll;

public sealed class ScrollSessionFactory : IScrollSessionFactory
{
    public ScrollSessionFactory(string profileName = StitchingProfiles.Current)
    {
        ProfileName = StitchingProfiles.Normalize(profileName);
        if (!StitchingProfiles.IsKnown(ProfileName))
        {
            throw new ArgumentException(
                $"Unknown stitching profile '{profileName}'. Supported values: {StitchingProfiles.Current}, {StitchingProfiles.SignalZoneExperiment}.",
                nameof(profileName));
        }
    }

    public string ProfileName { get; }

    public IScrollSession CreateSession()
    {
        return ProfileName switch
        {
            StitchingProfiles.Current => new ScrollSession(),
            StitchingProfiles.SignalZoneExperiment => new ScrollSession(new SignalZoneDetector(), new OverlapMatcher()),
            _ => throw new InvalidOperationException($"Unsupported stitching profile '{ProfileName}'."),
        };
    }
}
