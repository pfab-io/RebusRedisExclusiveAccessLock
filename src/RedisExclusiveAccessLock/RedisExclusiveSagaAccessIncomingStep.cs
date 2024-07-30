using Rebus.ExclusiveLocks;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Sagas;

namespace PFabIO.Rebus.Sagas.Exclusive.Redis;

//this class is partially copied from Rebus original EnforceExclusiveSagaAccessIncomingStep and EnforceExclusiveSagaAccessIncomingStepBase 
//https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/Exclusive/EnforceExclusiveSagaAccessIncomingStepBase.cs
//https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/Exclusive/EnforceExclusiveSagaAccessIncomingStep.cs
//to make the lock key safe with the full saga type name, the replication of this class was needed
public class RedisExclusiveSagaAccessIncomingStep(
    IExclusiveAccessLock lockHandler,
    string lockPrefix,
    CancellationToken cancellationToken,
    TimeSpan? lockSleepMinDelay = null,
    TimeSpan? lockSleepMaxDelay = null)
    : IIncomingStep
{
    private readonly SagaHelper _sagaHelper = new();
    private readonly TimeSpan _lockSleepMinDelay = lockSleepMinDelay ?? TimeSpan.FromMilliseconds(10);
    private readonly TimeSpan _lockSleepMaxDelay = lockSleepMaxDelay ?? TimeSpan.FromMilliseconds(20);
    private readonly Random _random = new(DateTime.UtcNow.GetHashCode());

    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var handlerInvokersForSagas = context.Load<HandlerInvokers>()
            .Where(l => l.HasSaga)
            .ToList();

        if (handlerInvokersForSagas.Count == 0)
        {
            await next();
            return;
        }

        var message = context.Load<Message>();
        var messageContext = MessageContext.Current;
        var body = message.Body;

        var correlationProperties = handlerInvokersForSagas
            .Select(h => h.Saga)
            .SelectMany(saga => _sagaHelper.GetCorrelationProperties(saga).ForMessage(body)
                .Select(correlationProperty => new { saga, correlationProperty }))
            .ToList();

        var locksToObtain = correlationProperties
            .Select(a => new
            {
                SagaType = a.saga.GetType().FullName,
                CorrelationPropertyName = a.correlationProperty.PropertyName,
                CorrelationPropertyValue = a.correlationProperty.GetValueFromMessage(messageContext, message)
            })
            .Select(a => $"{lockPrefix}:{a.SagaType}:{a.CorrelationPropertyName}:{a.CorrelationPropertyValue}")
            .Distinct() // avoid accidentally acquiring the same lock twice, because a bucket got hit more than once
            .OrderBy(str => str) // enforce consistent ordering to avoid deadlocks
            .ToArray();

        try
        {
            await WaitForLocks(locksToObtain);
            await next();
        }
        finally
        {
            await ReleaseLocks(locksToObtain);
        }
    }

    private async Task<bool> AcquireLockAsync(string lockId)
    {
        // We are done if we can get the lock
        if (await lockHandler.AcquireLockAsync(lockId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        // If we did not get the lock, we need to sleep and jitter the sleep period to avoid all
        // the locked threads waking up at the same time.
        var sleepRange = _lockSleepMaxDelay.TotalMilliseconds - _lockSleepMinDelay.TotalMilliseconds;
        var sleepTime = _lockSleepMinDelay + TimeSpan.FromMilliseconds(_random.NextDouble() * sleepRange);
        await Task.Delay(sleepTime, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private Task<bool> ReleaseLockAsync(string lockId)
    {
        return lockHandler.ReleaseLockAsync(lockId);
    }


    private async Task WaitForLocks(string[] lockIds)
    {
        foreach (var t in lockIds)
            while (!await AcquireLockAsync(t))
                await Task.Yield();
    }

    private async Task ReleaseLocks(string[] lockIds)
    {
        foreach (var t in lockIds)
            await ReleaseLockAsync(t);
    }
}