using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PFabIO.Rebus.Sagas.Exclusive.Redis.Extensions;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using StackExchange.Redis;
using TestConsole.HostedServices;
using SslSettings = Rebus.RabbitMq.SslSettings;

var hostBuilder = Host.CreateDefaultBuilder()
    .ConfigureServices((context, serviceCollection) =>
    {
        const string inputQueueName = "RedisRedisExclusiveAccessLockConsoleQueue";

        var connectionMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");

        var objectSerializer = new ObjectSerializer(_ => true);
        BsonSerializer.RegisterSerializer(objectSerializer);
        var client = new MongoClient("mongodb://localhost:27017/TestLocal?retryWrites=true&w=majority");
        var sagaMongoDatabase = client.GetDatabase("TestLocal");


        serviceCollection.AddRebus((configurer, _)
                => configurer.Transport(x => x
                        .UseRabbitMq(connectionString: "amqps://localhost",
                            inputQueueName: inputQueueName)
                        .Ssl(new SslSettings(enabled: false, serverName: null)))
                    .Sagas(x =>
                    {
                        x.StoreInMongoDb(mongoDatabase: sagaMongoDatabase);
                        x.EnforceExclusiveAccessWithRedis(connectionMultiplexer: connectionMultiplexer);
                    })
                    .Options(x =>
                    {
                        x.SetNumberOfWorkers(2);
                        x.SetMaxParallelism(10);
                    }).Routing(x => x.TypeBased()),
            onCreated: async bus =>
            {
                await bus.Subscribe<SagaRedisTestHostedService.TestExclusiveAccessSagaStartedEvent>();
            });

        serviceCollection.AddRebusHandler<SagaRedisTestHostedService.TestExclusiveAccessSaga>();
        serviceCollection.AddHostedService<SagaRedisTestHostedService>();
    });

using var build = hostBuilder.Build();

await build.RunAsync();