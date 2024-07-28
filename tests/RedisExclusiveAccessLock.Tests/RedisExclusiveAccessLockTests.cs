using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace PFabIO.Rebus.Sagas.Exclusive.Redis.Tests;

public class RedisExclusiveAccessLockTests
{
    private readonly IConnectionMultiplexer _connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();

    [Fact]
    public async Task AcquireLockAsync_LockAvailable_ExpectedTrue()
    {
        var database = Substitute.For<IDatabase>();
        _connectionMultiplexer.GetDatabase().Returns(database);

        database.LockTakeAsync(key: "key", value: "dummy", expiry: Arg.Any<TimeSpan>())
            .Returns(true);

        var redisExclusiveAccessLock = GetDefault(acquireLockWaitTimeSpan: null, lockExpirationTimeSpan: null,
            lockRenewIntervalTimeSpan: null);

        var acquireLockAsync = await redisExclusiveAccessLock.AcquireLockAsync("key", CancellationToken.None);

        acquireLockAsync.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_LockNotImmediatelyAvailable_ExpectedTrue()
    {
        var database = Substitute.For<IDatabase>();
        _connectionMultiplexer.GetDatabase().Returns(database);

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);

            database.LockTakeAsync(key: "key", value: "dummy", expiry: Arg.Any<TimeSpan>())
                .Returns(true);
        });

        var redisExclusiveAccessLock = GetDefault(acquireLockWaitTimeSpan: null, lockExpirationTimeSpan: null,
            lockRenewIntervalTimeSpan: null);

        var acquireLockAsync = await redisExclusiveAccessLock.AcquireLockAsync("key", CancellationToken.None);

        acquireLockAsync.Should().BeTrue();
    }
    
    [Fact]
    public async Task AcquireLockAsync_LockNotImmediatelyAvailableWithRenew_ExpectedTrue()
    {
        var database = Substitute.For<IDatabase>();
        _connectionMultiplexer.GetDatabase().Returns(database);

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);

            database.LockTakeAsync(key: "key", value: "dummy", expiry: Arg.Any<TimeSpan>())
                .Returns(true);
        });

        var redisExclusiveAccessLock = GetDefault(acquireLockWaitTimeSpan: null, lockExpirationTimeSpan: TimeSpan.FromMinutes(1),
            lockRenewIntervalTimeSpan: TimeSpan.FromMilliseconds(500));

        var acquireLockAsync = await redisExclusiveAccessLock.AcquireLockAsync("key", CancellationToken.None);

        acquireLockAsync.Should().BeTrue();
    }
    
    [Fact]
    public async Task IsLockAcquired_LockAcquired_ExpectedTrue()
    {
        var database = Substitute.For<IDatabase>();
        _connectionMultiplexer.GetDatabase().Returns(database);

        database.LockQueryAsync(key: "key")
            .Returns(new RedisValue("val"));

        var redisExclusiveAccessLock = GetDefault(acquireLockWaitTimeSpan: null, lockExpirationTimeSpan: null,
            lockRenewIntervalTimeSpan: null);

        var acquireLockAsync = await redisExclusiveAccessLock.IsLockAcquiredAsync("key", CancellationToken.None);

        acquireLockAsync.Should().BeTrue();
    }
    
    [Fact]
    public async Task IsLockAcquired_LockNotAcquired_ExpectedFalse()
    {
        var database = Substitute.For<IDatabase>();
        _connectionMultiplexer.GetDatabase().Returns(database);

        database.LockQueryAsync(key: "key")
            .Returns(new RedisValue());

        var redisExclusiveAccessLock = GetDefault(acquireLockWaitTimeSpan: null, lockExpirationTimeSpan: null,
            lockRenewIntervalTimeSpan: null);

        var acquireLockAsync = await redisExclusiveAccessLock.IsLockAcquiredAsync("key", CancellationToken.None);

        acquireLockAsync.Should().BeFalse();
    }

    private RedisExclusiveAccessLock GetDefault(TimeSpan? acquireLockWaitTimeSpan, TimeSpan? lockExpirationTimeSpan,
        TimeSpan? lockRenewIntervalTimeSpan)
        => new(connectionMultiplexer: _connectionMultiplexer, acquireLockWaitTimeSpan: acquireLockWaitTimeSpan,
            lockExpirationTimeSpan: lockExpirationTimeSpan, lockRenewIntervalTimeSpan: lockRenewIntervalTimeSpan);
}