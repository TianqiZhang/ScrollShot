using FluentAssertions;
using ScrollShot.Scroll;

namespace ScrollShot.Scroll.Tests;

public sealed class ScrollSessionFactoryTests
{
    [Theory]
    [InlineData(StitchingProfiles.Current)]
    [InlineData(StitchingProfiles.SignalZoneExperiment)]
    public void CreateSession_KnownProfiles_ReturnsSession(string profileName)
    {
        var factory = new ScrollSessionFactory(profileName);

        using var session = factory.CreateSession();

        session.Should().NotBeNull();
        factory.ProfileName.Should().Be(profileName);
    }

    [Fact]
    public void CreateSession_UnknownProfile_Throws()
    {
        var action = () => new ScrollSessionFactory("mystery-profile");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown stitching profile*");
    }
}
