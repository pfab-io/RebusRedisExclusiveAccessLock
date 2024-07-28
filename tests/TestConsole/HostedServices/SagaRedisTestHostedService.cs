using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Sagas;

namespace TestConsole.HostedServices;

public class SagaRedisTestHostedService(IBus bus, ILogger<SagaRedisTestHostedService> logging) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logging.LogInformation("SagaRedisTestHostedService Started");

        Console.WriteLine("Would you like to publish messages? (y/n)");
        var readLine = Console.ReadLine();

        if (!string.Equals(readLine, "y", StringComparison.InvariantCultureIgnoreCase))
            return;

        foreach (var i in Enumerable.Range(1, 10))
            await bus.SendLocal(new StartTestExclusiveAccessSagaCommand
                { SagaId = i });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public class TestExclusiveAccessSaga(ILogger<TestExclusiveAccessSaga> logger, IBus bus)
        : Saga<TestExclusiveAccessSagaData>,
            IAmInitiatedBy<StartTestExclusiveAccessSagaCommand>,
            IHandleMessages<TestExclusiveAccessSagaStartedEvent>,
            IHandleMessages<IFailed<TestExclusiveAccessSagaStartedEvent>>
    {
        protected override void CorrelateMessages(ICorrelationConfig<TestExclusiveAccessSagaData> config)
        {
            config.Correlate<StartTestExclusiveAccessSagaCommand>(x => x.SagaId, x => x.SagaId);
            config.Correlate<TestExclusiveAccessSagaStartedEvent>(x => x.SagaId, x => x.SagaId);
        }


        public async Task Handle(StartTestExclusiveAccessSagaCommand message)
        {
            logger.LogInformation("Message received {OperationId}", message.SagaId);

            foreach (var i in Enumerable.Range(1, 5))
                await bus.Publish(new TestExclusiveAccessSagaStartedEvent
                {
                    Index = i,
                    SagaId = message.SagaId
                });
        }

        public async Task Handle(TestExclusiveAccessSagaStartedEvent message)
        {
            logger.LogInformation("Event received {Index} {Events} {OperationId}", message.Index, Data.Events,
                message.SagaId);

            await Task.Delay(5000);

            Data.Events++;

            if (Data.Events == 5)
            {
                logger.LogInformation("Marking as complete {Events} {OperationId}", Data.Events, message.SagaId);
                MarkAsComplete();
            }

            logger.LogInformation("Event Handled {Index} {Events} {OperationId}", message.Index, Data.Events,
                message.SagaId);
        }

        public Task Handle(IFailed<TestExclusiveAccessSagaStartedEvent> message)
        {
            logger.LogError("Error handling TestExclusiveAccessSagaStartedEvent {OperationId}",
                message.Message.SagaId);

            return Task.CompletedTask;
        }
    }

    public class TestExclusiveAccessSagaData : SagaData
    {
        public int SagaId { get; init; }

        public int Events { get; set; }
    }

    public class StartTestExclusiveAccessSagaCommand
    {
        public required int SagaId { get; init; }
    }

    public class TestExclusiveAccessSagaStartedEvent
    {
        public required int SagaId { get; init; }
        public required int Index { get; init; }
    }
}