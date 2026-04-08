using FluentAssertions;

namespace ScrollShot.App.Tests;

public sealed class Phase0SmokeTests
{
    [Fact]
    public void AppTestProject_IsWiredIntoSolution()
    {
        typeof(Phase0SmokeTests).Assembly.GetName().Name.Should().Be("ScrollShot.App.Tests");
    }
}
