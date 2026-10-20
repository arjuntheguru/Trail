using Trail.Application.Features.Integrations;

namespace Trail.Infrastructure.Integrations;

internal sealed record WebhookSendResult(bool Succeeded, int? StatusCode, string? Error);

/// <summary>Performs one signed POST through the webhook HTTP client (timeout, no redirects, no proxy, SSRF-safe connect).</summary>
internal sealed class WebhookSender(IHttpClientFactory httpClients, TimeProvider clock)
{
    public async Task<WebhookSendResult> SendAsync(
        string url, string eventType, Guid deliveryId, string secret, string body, CancellationToken ct)
    {
        try
        {
            var client = httpClients.CreateClient(WebhookOptions.HttpClientName);
            using var request = WebhookSigning.CreateRequest(url, eventType, deliveryId, secret, body, clock.GetUtcNow());
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            var code = (int)response.StatusCode;
            return WebhookRetryPolicy.IsSuccess(code)
                ? new WebhookSendResult(true, code, null)
                : new WebhookSendResult(false, code, $"The endpoint answered HTTP {code}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new WebhookSendResult(false, null, "The endpoint did not answer within the time limit.");
        }
        catch (Exception ex)
        {
            return new WebhookSendResult(false, null, ex.GetBaseException().Message);
        }
    }
}
