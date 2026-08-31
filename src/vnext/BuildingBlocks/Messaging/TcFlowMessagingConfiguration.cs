using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;
using Wolverine.Persistence;

namespace VietAIS.TCFlow.BuildingBlocks.Messaging;

public static class TcFlowMessagingConfiguration
{
    public static void Configure(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnvelopeIdGeneration = EnvelopeIdGeneration.GuidV7;
        options.UseFluentValidation();
        options.Policies.AutoApplyTransactions(IdempotencyStyle.Eager);
        options.Policies.UseDurableLocalQueues();
        options.Policies.UseDurableInboxOnAllListeners();
        options.Policies.UseDurableOutboxOnAllSendingEndpoints();
        options.Policies
            .OnException<TimeoutException>()
            .RetryWithCooldown(
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(2));
    }
}
