namespace BuildingBlocks.Application.Abstractions;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
    Task SendOtpAsync(string phoneNumber, CancellationToken ct = default);
}
