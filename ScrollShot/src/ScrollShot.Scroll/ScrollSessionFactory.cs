using ScrollShot.Scroll.Profiles.Bidirectional;
using ScrollShot.Scroll.Profiles.Current;
using ScrollShot.Scroll.Profiles.Signal;

namespace ScrollShot.Scroll;

public sealed class ScrollSessionFactory : IScrollSessionFactory
{
    public ScrollSessionFactory(string profileName = StitchingProfiles.Current)
    {
        ProfileName = StitchingProfiles.Normalize(profileName);
        if (!StitchingProfiles.IsKnown(ProfileName))
        {
            throw new ArgumentException(
                $"Unknown stitching profile '{profileName}'. Supported values: {StitchingProfiles.Current}, {StitchingProfiles.SignalZoneExperiment}, {StitchingProfiles.SignalHybridExperiment}, {StitchingProfiles.BidirectionalCurrentExperiment}.",
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
            StitchingProfiles.SignalHybridExperiment => new ScrollSession(new SignalZoneDetector(), new SignalHybridOverlapMatcher()),
            StitchingProfiles.BidirectionalCurrentExperiment => new BidirectionalScrollSession(new ZoneDetector(), new BidirectionalOverlapMatcher(new OverlapMatcher())),
            _ => throw new InvalidOperationException($"Unsupported stitching profile '{ProfileName}'."),
        };
    }
}
