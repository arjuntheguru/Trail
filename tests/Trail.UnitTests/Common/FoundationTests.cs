using NSubstitute;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Infrastructure.Tenancy;

namespace Trail.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Failed_result_exposes_error_and_refuses_value()
    {
        Result<int> result = Error.NotFound("site.not_found", "Site not found.");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Successful_result_carries_value()
    {
        Result<string> result = "ok";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("ok");
    }
}

public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 0, 1, PageRequest.DefaultPageSize)]
    [InlineData(-3, -1, 1, PageRequest.DefaultPageSize)]
    [InlineData(2, 500, 2, PageRequest.MaxPageSize)]
    [InlineData(3, 10, 3, 10)]
    public void Clamps_out_of_range_values(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PageRequest(page, size);

        request.SafePage.ShouldBe(expectedPage);
        request.SafePageSize.ShouldBe(expectedSize);
        request.Skip.ShouldBe((expectedPage - 1) * expectedSize);
    }

    [Fact]
    public void Total_pages_rounds_up_and_handles_empty()
    {
        new PagedResult<int>([], 1, 25, 0).TotalPages.ShouldBe(0);
        new PagedResult<int>([1], 1, 25, 26).TotalPages.ShouldBe(2);
        new PagedResult<int>([1], 2, 25, 26).HasNextPage.ShouldBeFalse();
    }
}

public sealed class TenantContextTests
{
    private static ICurrentUser User(Guid? org, bool platformAdmin = false)
    {
        var user = Substitute.For<ICurrentUser>();
        user.OrganizationId.Returns(org);
        user.IsPlatformAdmin.Returns(platformAdmin);
        return user;
    }

    [Fact]
    public void Defaults_to_the_current_users_organization()
    {
        var org = Guid.CreateVersion7();

        new TenantContext(User(org)).OrganizationId.ShouldBe(org);
    }

    [Fact]
    public void Background_work_without_a_user_can_choose_an_organization()
    {
        var org = Guid.CreateVersion7();
        var tenant = new TenantContext(User(null));

        tenant.UseOrganization(org);

        tenant.OrganizationId.ShouldBe(org);
    }

    [Fact]
    public void A_member_cannot_switch_to_another_organization()
    {
        var tenant = new TenantContext(User(Guid.CreateVersion7()));

        Should.Throw<TenantIsolationException>(() => tenant.UseOrganization(Guid.CreateVersion7()));
    }

    [Fact]
    public void A_platform_admin_can_act_in_another_organization()
    {
        var other = Guid.CreateVersion7();
        var tenant = new TenantContext(User(Guid.CreateVersion7(), platformAdmin: true));

        tenant.UseOrganization(other);

        tenant.OrganizationId.ShouldBe(other);
    }
}
