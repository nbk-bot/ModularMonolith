using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
// CommandHandler attribute lives in ActualLab.CommandR.Configuration.
using FluentValidation;

namespace BuildingBlocks.Infrastructure.Commands;

/// <summary>
/// Fusion CommandR filter that runs FluentValidation against every command
/// before any business handler. Registered as an open-generic
/// <see cref="ICommandHandler{TCommand}"/> in <c>AddBuildingBlocks</c> so it
/// transparently wraps every <see cref="ICommand"/> dispatched through Fusion.
/// Replaces the old MediatR <c>ValidationBehavior&lt;,&gt;</c> pipeline.
/// </summary>
public sealed class FluentValidationCommandHandler<TCommand>(IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    [CommandHandler(Priority = 1_000_000, IsFilter = true)]
    public async Task OnCommand(TCommand command, CommandContext context, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var ctx = new ValidationContext<TCommand>(command);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0) throw new ValidationException(failures);
        }

        await context.InvokeRemainingHandlers(cancellationToken);
    }
}
