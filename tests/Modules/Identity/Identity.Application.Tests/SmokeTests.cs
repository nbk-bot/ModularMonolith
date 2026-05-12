using FluentAssertions;
using Xunit;

namespace Identity.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void Smoke_ShouldPass()
    {
        true.Should().BeTrue();
    }
}
