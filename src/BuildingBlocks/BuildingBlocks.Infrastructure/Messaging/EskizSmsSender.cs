using BuildingBlocks.Application.Abstractions;
using Messager.EskizUz;

namespace BuildingBlocks.Infrastructure.Messaging;

internal sealed class EskizSmsSender(MessagerAgent agent) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
        => agent.SendSMSAsync(phoneNumber, message);

    public Task SendOtpAsync(string phoneNumber, CancellationToken ct = default)
        => agent.SendOtpAsync(phoneNumber);
}

public sealed class EskizOptions
{
    public const string SectionName = "Eskiz";
    public string Email { get; init; } = "";
    public string SecretKey { get; init; } = "";
}
