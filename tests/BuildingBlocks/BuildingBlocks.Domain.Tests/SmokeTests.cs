using FluentAssertions;
using Xunit;

namespace BuildingBlocks.Domain.Tests;

public class SmokeTests
{
    [Fact]
    public void Smoke_ShouldPass()
    {
        true.Should().BeTrue();
    }
}
