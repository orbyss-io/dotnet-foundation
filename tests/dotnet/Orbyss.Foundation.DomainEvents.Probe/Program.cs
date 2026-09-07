using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.DomainEvents;

await VerifyAwaitedSequentialNestedDispatchAsync();
await VerifyFailurePropagationAsync();
await VerifyDepthProtectionAsync();

static async Task VerifyAwaitedSequentialNestedDispatchAsync()
{
    var services = new ServiceCollection();
    services.AddSingleton<Recorder>();
    services.AddFoundationDomainEvents();
    services.AddScoped<IDomainEventHandler<RootEvent>, FirstRootHandler>();
    services.AddScoped<IDomainEventHandler<RootEvent>, NestedRootHandler>();
    services.AddScoped<IDomainEventHandler<RootEvent>, LastRootHandler>();
    services.AddScoped<IDomainEventHandler<ChildEvent>, ChildHandler>();
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

    await publisher.PublishAsync(new RootEvent());

    var recorder = scope.ServiceProvider.GetRequiredService<Recorder>();
    Require(recorder.Steps.SequenceEqual(["root:first", "child", "root:last"]), "handlers were not awaited sequentially");
    var root = recorder.Contexts[0];
    var child = recorder.Contexts[1];
    Require(child.DispatchId == root.DispatchId, "nested publication changed the dispatch identity");
    Require(child.CausationId == root.PublicationId, "nested publication lost causation metadata");
    Require(child.Depth == 1, "nested publication depth was not recorded");
}

static async Task VerifyFailurePropagationAsync()
{
    var services = new ServiceCollection();
    services.AddSingleton<Recorder>();
    services.AddFoundationDomainEvents();
    services.AddScoped<IDomainEventHandler<FailureEvent>, ThrowingHandler>();
    services.AddScoped<IDomainEventHandler<FailureEvent>, ForbiddenLateHandler>();
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

    try
    {
        await publisher.PublishAsync(new FailureEvent());
        throw new InvalidOperationException("handler failure did not propagate");
    }
    catch (ExpectedFailureException)
    {
    }

    var recorder = scope.ServiceProvider.GetRequiredService<Recorder>();
    Require(recorder.Steps.SequenceEqual(["failure:throw"]), "dispatch continued after a handler failure");
}

static async Task VerifyDepthProtectionAsync()
{
    var services = new ServiceCollection();
    services.AddFoundationDomainEvents(options => options.MaximumDepth = 0);
    services.AddScoped<IDomainEventHandler<RecursiveEvent>, RecursiveHandler>();
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

    try
    {
        await publisher.PublishAsync(new RecursiveEvent());
        throw new InvalidOperationException("nested dispatch exceeded its bound without failing");
    }
    catch (InvalidOperationException error) when (error.Message.Contains("MaximumDepth", StringComparison.Ordinal))
    {
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record RootEvent : IDomainEvent;

sealed record ChildEvent : IDomainEvent;

sealed record FailureEvent : IDomainEvent;

sealed record RecursiveEvent : IDomainEvent;

sealed class Recorder
{
    public List<string> Steps { get; } = [];

    public List<DomainEventContext> Contexts { get; } = [];
}

sealed class FirstRootHandler(Recorder recorder) : IDomainEventHandler<RootEvent>
{
    public ValueTask HandleAsync(RootEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken)
    {
        recorder.Steps.Add("root:first");
        recorder.Contexts.Add(context);
        return ValueTask.CompletedTask;
    }
}

sealed class NestedRootHandler(IDomainEventPublisher publisher) : IDomainEventHandler<RootEvent>
{
    public ValueTask HandleAsync(RootEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken) =>
        publisher.PublishAsync(new ChildEvent(), cancellationToken);
}

sealed class LastRootHandler(Recorder recorder) : IDomainEventHandler<RootEvent>
{
    public ValueTask HandleAsync(RootEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken)
    {
        recorder.Steps.Add("root:last");
        return ValueTask.CompletedTask;
    }
}

sealed class ChildHandler(Recorder recorder) : IDomainEventHandler<ChildEvent>
{
    public ValueTask HandleAsync(ChildEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken)
    {
        recorder.Steps.Add("child");
        recorder.Contexts.Add(context);
        return ValueTask.CompletedTask;
    }
}

sealed class ThrowingHandler(Recorder recorder) : IDomainEventHandler<FailureEvent>
{
    public ValueTask HandleAsync(FailureEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken)
    {
        recorder.Steps.Add("failure:throw");
        throw new ExpectedFailureException();
    }
}

sealed class ForbiddenLateHandler(Recorder recorder) : IDomainEventHandler<FailureEvent>
{
    public ValueTask HandleAsync(FailureEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken)
    {
        recorder.Steps.Add("failure:late");
        return ValueTask.CompletedTask;
    }
}

sealed class RecursiveHandler(IDomainEventPublisher publisher) : IDomainEventHandler<RecursiveEvent>
{
    public ValueTask HandleAsync(RecursiveEvent domainEvent, DomainEventContext context, CancellationToken cancellationToken) =>
        publisher.PublishAsync(domainEvent, cancellationToken);
}

sealed class ExpectedFailureException : Exception;
