using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace ReproTestHarnessFailureWithEndPoint;

[TestClass]
public class Repro
{
    [TestMethod]
    [DataRow("queue")]
    [DataRow("foo/queue")]
    public async Task WillNeverComplete(string endpoint)
    {
        var sync = new SemaphoreSlim(0);

        var services = new ServiceCollection()
            .AddSingleton(sync)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<Foo>().Endpoint(e => e.Name = endpoint);
                cfg.UsingInMemory((ctx, mem) =>
                {
                    mem.ConfigureEndpoints(ctx);
                    mem.UseInMemoryOutbox();
                });
            })
            .BuildServiceProvider();

        var harness = services.GetRequiredService<ITestHarness>();
        await harness.Start();

        await services.GetRequiredService<IBus>().Publish(new FooMessage());
        await sync.WaitAsync();
    }
}

public class FooMessage { }
public class Foo : IConsumer<FooMessage>
{
    private readonly SemaphoreSlim _sync;

    public Foo(SemaphoreSlim sync)
    {
        _sync = sync;
    }

    public Task Consume(ConsumeContext<FooMessage> context)
    {
        Console.WriteLine($"Consuming: {context.Message.GetType().Name}");
        _sync.Release();

        return Task.CompletedTask;
    }
}