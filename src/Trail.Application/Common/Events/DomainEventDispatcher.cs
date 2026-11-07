using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Trail.Domain.Common;

namespace Trail.Application.Common.Events;

internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Invokers = new();

    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
            await Invokers.GetOrAdd(domainEvent.GetType(), CreateInvoker)(services, domainEvent, ct);
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateInvoker(Type eventType) =>
        typeof(DomainEventDispatcher)
            .GetMethod(nameof(InvokeAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(eventType)
            .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task InvokeAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.Handle((TEvent)domainEvent, ct);
    }
}
