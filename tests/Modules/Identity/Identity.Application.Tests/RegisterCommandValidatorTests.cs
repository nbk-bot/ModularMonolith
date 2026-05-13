using FluentAssertions;
using FluentValidation.TestHelper;
using Identity.Application.Features.Register;

namespace Identity.Application.Tests;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _sut = new();

    [Theory]
    [InlineData("user@example.com", "Password1!", "Najim")]
    [InlineData("a@b.uz", "12345678", null)]
    public void Should_Pass_For_Valid_Inputs(string email, string password, string? fullName)
    {
        var result = _sut.TestValidate(new RegisterCommand(email, password, fullName));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Fail_When_Email_Empty()
    {
        var result = _sut.TestValidate(new RegisterCommand("", "Password1!", "X"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-domain.com")]
    [InlineData("user@")]
    public void Should_Fail_When_Email_Invalid(string email)
    {
        var result = _sut.TestValidate(new RegisterCommand(email, "Password1!", null));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_Fail_When_Password_TooShort(string password)
    {
        var result = _sut.TestValidate(new RegisterCommand("u@e.uz", password, null));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Pass_When_FullName_Null()
    {
        var result = _sut.TestValidate(new RegisterCommand("u@e.uz", "Password1!", null));
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }
}
