using FluentAssertions;
using Xunit;

namespace BuildingBlocks.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void Smoke_ShouldPass()
    {
        true.Should().BeTrue();
    }
}
