using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class IntegrationAccessTests
{
    [Fact]
    public void Callers_with_access_to_all_sites_may_manage_integrations() =>
        IntegrationAccess.RequireAllSites(SiteScope.Everything).ShouldBeNull();

    [Fact]
    public void Callers_limited_to_some_sites_are_refused()
    {
        var scope = new SiteScope(false, new HashSet<Guid> { Guid.CreateVersion7() }, new HashSet<Guid>());

        IntegrationAccess.RequireAllSites(scope)!.Type.ShouldBe(ErrorType.Forbidden);
        IntegrationAccess.RequireAllSites(SiteScope.Nothing)!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void Insecure_webhook_targets_are_only_allowed_in_development_and_testing(string environment, bool allowed) =>
        WebhookOptions.InsecureTargetsAllowedIn(environment).ShouldBe(allowed);
}
