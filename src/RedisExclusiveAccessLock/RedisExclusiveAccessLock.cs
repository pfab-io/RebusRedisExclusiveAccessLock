using Rebus.ExclusiveLocks;
using StackExchange.Redis;

namespace PFabIO.Rebus.Sagas.Exclusive.Redis;

public class RedisExclusiveAccessLock(
    IConnectionMultiplexer connectionMultiplexer,
    TimeSpan? acquireLockWaitTimeSpan = null,
    TimeSpan? lockExpirationTimeSpan = null,
    TimeSpan? lockRenewIntervalTimeSpan = null)
    : IExclusiveAccessLock
{
    private readonly TimeSpan _acquireLockWaitTimeSpan = acquireLockWaitTimeSpan ?? TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _lockExpirationTimeSpan = lockExpirationTimeSpan ?? TimeSpan.FromMinutes(1);
    private readonly TimeSpan _lockRenewIntervalTimeSpan = lockRenewIntervalTimeSpan ?? TimeSpan.FromSeconds(10);
    private const string DummyValue = "dummy";

    private CancellationTokenSource _renewalCancellationTokenSource = null!;

    public async Task<bool> AcquireLockAsync(string key, CancellationToken cancellationToken)
    {
        var db = connectionMultiplexer.GetDatabase();
        while (!await db.LockTakeAsync(key: key, value: DummyValue, expiry: _lockExpirationTimeSpan))
            await Task.Delay(_acquireLockWaitTimeSpan, cancellationToken);

        _renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = RenewLockAsync(key: key, cancellationToken: _renewalCancellationTokenSource.Token);

        return true;
    }

    public async Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken)
    {
        var db = connectionMultiplexer.GetDatabase();
        var l = await db.LockQueryAsync(key);
        return l.HasValue;
    }

    public async Task<bool> ReleaseLockAsync(string key)
    {
        await _renewalCancellationTokenSource.CancelAsync();
        var db = connectionMultiplexer.GetDatabase();
        return await db.LockReleaseAsync(key, DummyValue);
    }

    private async Task RenewLockAsync(string key, CancellationToken cancellationToken)
    {
        var db = connectionMultiplexer.GetDatabase();
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_lockRenewIntervalTimeSpan, cancellationToken);
            await db.LockExtendAsync(key, DummyValue, _lockExpirationTimeSpan);
        }
    }
}