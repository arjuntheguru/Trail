using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Trail.Application.Features.Departments;
using Trail.Application.Features.Members;
using Trail.Application.Features.Regions;
using Trail.Application.Features.Sites;

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>JSON helpers that match the API's serializer settings (camelCase, enums as strings).</summary>
public static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url) =>
        client.PostAsJsonAsync(url, new { }, Json, Ct);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> PostEmptyAsync(this HttpClient client, string url) =>
        client.PostAsync(url, null, Ct);

    /// <summary>Deserializes a successful response; fails the test with the body when the call did not succeed.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        if (!response.IsSuccessStatusCode)
            throw new ShouldAssertException($"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)
            ?? throw new InvalidOperationException($"Empty response body ({(int)response.StatusCode}): {body}");
    }

    /// <summary>The stable "code" of a problem response, e.g. "site.name_taken".</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
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

    // Seeding through the real API keeps tests honest about what a client can do.

    public static async Task<RegionResponse> CreateRegionAsync(this HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand(name, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<RegionResponse>();
    }

    public static async Task<DepartmentResponse> CreateDepartmentAsync(this HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand(name));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<DepartmentResponse>();
    }

    public static async Task<SiteResponse> CreateSiteAsync(this HttpClient client, string name, Guid? regionId = null)
    {
        var response = await client.PostJsonAsync("/api/v1/sites",
            new CreateSiteCommand(name, null, regionId, null, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<SiteResponse>();
    }

    public static async Task SetAccessAsync(
        this HttpClient client, Guid membershipId, bool allSites,
        Guid[]? siteIds = null, Guid[]? regionIds = null, Guid[]? departmentIds = null)
    {
        var response = await client.PutJsonAsync($"/api/v1/members/{membershipId}/access",
            new SetMemberAccessCommand(allSites, siteIds, regionIds, departmentIds));
        response.EnsureSuccessStatusCode();
    }
}
