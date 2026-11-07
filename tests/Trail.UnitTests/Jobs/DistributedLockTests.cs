using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Jobs;
using Trail.Infrastructure.Locking;
using Trail.Infrastructure.Persistence;

namespace Trail.UnitTests.Jobs;

public class DistributedLockTests
{
    [Fact]
    public void Lock_names_are_prefixed_and_trimmed()
    {
        MySqlDistributedLock.LockName(" migrations ").ShouldBe("trail:migrations");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_lock_names_are_rejected(string name) =>
        Should.Throw<ArgumentException>(() => MySqlDistributedLock.LockName(name));

    [Fact]
    public void Lock_names_stay_within_the_mysql_limit()
    {
        var longest = new string('a', MySqlDistributedLock.MaxNameLength - MySqlDistributedLock.Prefix.Length);
        MySqlDistributedLock.LockName(longest).Length.ShouldBe(64);
        Should.Throw<ArgumentException>(() => MySqlDistributedLock.LockName(longest + "a"));
    }

    private static IDistributedLock LockReturning(IAsyncDisposable? handle)
    {
        var locks = Substitute.For<IDistributedLock>();
        locks.TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(handle));
        return locks;
    }

    [Fact]
    public async Task A_tick_is_skipped_when_the_lock_is_held_elsewhere()
    {
        var locks = LockReturning(null);
        var ran = false;

        var result = await ExclusiveTick.RunAsync(locks, "daily-digest", NullLogger.Instance, _ => { ran = true; return Task.CompletedTask; }, CancellationToken.None);

        result.ShouldBeFalse();
        ran.ShouldBeFalse();
        await locks.Received(1).TryAcquireAsync("job:daily-digest", TimeSpan.Zero, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_tick_runs_and_releases_the_lock_when_acquired()
    {
        var handle = Substitute.For<IAsyncDisposable>();
        var locks = LockReturning(handle);
        var ran = false;

        var result = await ExclusiveTick.RunAsync(locks, "task-generation", NullLogger.Instance, _ => { ran = true; return Task.CompletedTask; }, CancellationToken.None);

        result.ShouldBeTrue();
        ran.ShouldBeTrue();
        await handle.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task The_lock_is_released_when_the_tick_throws()
    {
        var handle = Substitute.For<IAsyncDisposable>();
        var locks = LockReturning(handle);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ExclusiveTick.RunAsync(locks, "x", NullLogger.Instance, _ => throw new InvalidOperationException(), CancellationToken.None));

        await handle.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task The_loop_survives_a_failing_tick_and_ends_quietly_on_shutdown()
    {
        using var cts = new CancellationTokenSource();
        var clock = new FakeTimeProvider();
        var calls = 0;
        var secondTick = new TaskCompletionSource();

        var loop = ExclusiveTick.LoopAsync(TimeSpan.FromMinutes(1), clock, NullLogger.Instance, "job", _ =>
        {
            if (++calls == 2) secondTick.SetResult();
            throw new InvalidOperationException("boom");
        }, cts.Token);

        clock.Advance(TimeSpan.FromMinutes(1));
        await secondTick.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await loop.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); // does not throw
        calls.ShouldBe(2);
    }
}

public class DatabaseOptionsTests
{
    private static bool IsValid(DatabaseOptions o) =>
        Validator.TryValidateObject(o, new ValidationContext(o), [], validateAllProperties: true);

    [Fact]
    public void Defaults_are_valid_with_a_connection_string() =>
        IsValid(new DatabaseOptions { ConnectionString = "Server=x" }).ShouldBeTrue();

    [Fact]
    public void A_connection_string_is_required() => IsValid(new DatabaseOptions()).ShouldBeFalse();

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Command_timeout_is_limited(int seconds, bool valid) =>
        IsValid(new DatabaseOptions { ConnectionString = "Server=x", CommandTimeoutSeconds = seconds }).ShouldBe(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(300, true)]
    [InlineData(3601, false)]
    public void Migration_lock_wait_is_limited(int seconds, bool valid) =>
        IsValid(new DatabaseOptions { ConnectionString = "Server=x", MigrationLockWaitSeconds = seconds }).ShouldBe(valid);
}
