using System.Net;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Trail.Application.Features.Integrations;
using Trail.Infrastructure.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class WebhookSenderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }

    private static (WebhookSender Sender, StubHandler Handler) Create(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHandler(respond);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WebhookOptions.HttpClientName).Returns(_ => new HttpClient(handler));
        return (new WebhookSender(factory, new FakeTimeProvider(Now)), handler);
    }

    [Fact]
    public async Task Success_posts_the_signed_body_with_headers()
    {
        var (sender, handler) = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var deliveryId = Guid.CreateVersion7();

        var result = await sender.SendAsync("https://hooks.example.com/x", "task.completed", deliveryId, "s3cret", "{\"a\":1}", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.StatusCode.ShouldBe(204);
        handler.Body.ShouldBe("{\"a\":1}");
        handler.Request!.Headers.GetValues("Trail-Event").ShouldBe(["task.completed"]);
        handler.Request.Headers.GetValues("Trail-Delivery").ShouldBe([deliveryId.ToString()]);
        handler.Request.Headers.GetValues("Trail-Timestamp").ShouldBe(["1700000000"]);
        handler.Request.Headers.GetValues("Trail-Signature").ShouldBe([WebhookSigning.Sign("s3cret", 1700000000, "{\"a\":1}")]);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    public async Task Non_2xx_is_a_failure_that_keeps_the_status(HttpStatusCode status)
    {
        var (sender, _) = Create(_ => Task.FromResult(new HttpResponseMessage(status)));

        var result = await sender.SendAsync("https://hooks.example.com/x", "ping", Guid.NewGuid(), "s", "{}", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.StatusCode.ShouldBe((int)status);
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task Exceptions_are_failures_not_crashes()
    {
        var (sender, _) = Create(_ => throw new HttpRequestException("Refusing to connect to 'evil.test'."));

        var result = await sender.SendAsync("https://evil.test/x", "ping", Guid.NewGuid(), "s", "{}", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.StatusCode.ShouldBeNull();
        result.Error!.ShouldContain("Refusing to connect");
    }

    [Fact]
    public async Task A_timeout_is_a_failure_with_a_clear_message()
    {
        var (sender, _) = Create(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        var result = await sender.SendAsync("https://slow.test/x", "ping", Guid.NewGuid(), "s", "{}", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("time limit");
    }

    [Fact]
    public async Task Shutdown_cancellation_is_not_swallowed()
    {
        using var cts = new CancellationTokenSource();
        var (sender, _) = Create(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Should.ThrowAsync<OperationCanceledException>(
            () => sender.SendAsync("https://hooks.example.com/x", "ping", Guid.NewGuid(), "s", "{}", cts.Token));
    }
}
