using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trail.IntegrationTests.Infrastructure;

public static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json, Ct);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        if (!response.IsSuccessStatusCode)
            throw new ShouldAssertException($"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    /// <summary>Asserts the status code and, for problem responses, the stable error code.</summary>
    public static async Task ShouldBeAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(status, body);
        if (code is not null)
        {
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("code").GetString().ShouldBe(code, body);
        }
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
