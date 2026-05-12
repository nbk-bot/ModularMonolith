using FluentAssertions;
using Xunit;

namespace Identity.Domain.Tests;

public class SmokeTests
{
    [Fact]
    public void Smoke_ShouldPass()
    {
        true.Should().BeTrue();
    }
}
