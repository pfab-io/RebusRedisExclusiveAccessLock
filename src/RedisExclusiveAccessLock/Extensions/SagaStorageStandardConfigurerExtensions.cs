using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Sagas;
using StackExchange.Redis;

namespace PFabIO.Rebus.Sagas.Exclusive.Redis.Extensions;

public static class SagaStorageStandardConfigurerExtensions
{
    public static void EnforceExclusiveAccessWithRedis(this StandardConfigurer<ISagaStorage> configurer,
        IConnectionMultiplexer connectionMultiplexer,
        string lockPrefix = "sagalock_")
    {
        var redisExclusiveAccessLock = new RedisExclusiveAccessLock(connectionMultiplexer);

        configurer
            .OtherService<IPipeline>()
            .Decorate(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var cancellationToken = c.Get<CancellationToken>();
                var step = new RedisExclusiveSagaAccessIncomingStep(lockHandler: redisExclusiveAccessLock,
                    lockPrefix: lockPrefix,
                    cancellationToken: cancellationToken);

                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.Before, typeof(LoadSagaDataStep));
            });
    }
}