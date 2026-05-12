using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests;

public class SmokeTests
{
    [Fact]
    public void Smoke_ShouldPass()
    {
        true.Should().BeTrue();
    }
}
