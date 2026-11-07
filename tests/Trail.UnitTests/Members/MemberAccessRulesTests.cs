using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Me;
using Trail.Application.Features.Members;

namespace Trail.UnitTests.Members;

public sealed class MemberAccessRulesTests
{
    private static readonly Guid SiteA = Guid.CreateVersion7();
    private static readonly Guid SiteB = Guid.CreateVersion7();
    private static readonly Guid RegionA = Guid.CreateVersion7();
    private static readonly Guid RegionB = Guid.CreateVersion7();

    private static SiteScope Limited() =>
        new(false, new HashSet<Guid> { SiteA }, new HashSet<Guid> { RegionA });

    [Fact]
    public void Callers_with_all_sites_may_grant_anything() =>
        MemberAccessRules.IsOutOfScope(SiteScope.Everything, true, [SiteA, SiteB], [RegionB]).ShouldBeFalse();

    [Fact]
    public void Limited_callers_may_grant_their_own_sites_and_regions() =>
        MemberAccessRules.IsOutOfScope(Limited(), false, [SiteA], [RegionA]).ShouldBeFalse();

    [Fact]
    public void Limited_callers_may_grant_nothing_at_all() =>
        MemberAccessRules.IsOutOfScope(Limited(), false, [], []).ShouldBeFalse();

    [Fact]
    public void Limited_callers_cannot_grant_all_sites() =>
        MemberAccessRules.IsOutOfScope(Limited(), true, [], []).ShouldBeTrue();

    [Fact]
    public void Limited_callers_cannot_grant_sites_or_regions_they_lack()
    {
        MemberAccessRules.IsOutOfScope(Limited(), false, [SiteA, SiteB], []).ShouldBeTrue();
        MemberAccessRules.IsOutOfScope(Limited(), false, [], [RegionB]).ShouldBeTrue();
        MemberAccessRules.IsOutOfScope(SiteScope.Nothing, false, [SiteA], []).ShouldBeTrue();
    }

    [Fact]
    public void Error_codes_match_the_access_endpoint()
    {
        MemberAccessRules.OutOfScope.Code.ShouldBe("member.access_out_of_scope");
        MemberAccessRules.UnknownIds.Code.ShouldBe("member.unknown_ids");
    }

    [Fact]
    public void Invitation_validator_accepts_no_ids_and_valid_ids()
    {
        var validator = new InviteMemberValidator();
        validator.Validate(new InviteMemberCommand("a@example.com", Guid.CreateVersion7())).IsValid.ShouldBeTrue();
        validator.Validate(new InviteMemberCommand("a@example.com", Guid.CreateVersion7(), AllSites: false,
            SiteIds: [SiteA], RegionIds: [RegionA])).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invitation_validator_rejects_empty_guids_and_oversized_lists()
    {
        var validator = new InviteMemberValidator();
        var roleId = Guid.CreateVersion7();

        validator.Validate(new InviteMemberCommand("a@example.com", roleId, SiteIds: [Guid.Empty])).IsValid.ShouldBeFalse();
        validator.Validate(new InviteMemberCommand("a@example.com", roleId, RegionIds: [Guid.Empty])).IsValid.ShouldBeFalse();
        validator.Validate(new InviteMemberCommand("a@example.com", roleId,
            SiteIds: Enumerable.Range(0, MemberAccessRules.MaxAssignments + 1).Select(_ => Guid.CreateVersion7()).ToList()))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Access_validator_applies_the_same_rules()
    {
        var validator = new SetMemberAccessValidator();
        validator.Validate(new SetMemberAccessCommand(false, [SiteA], [RegionA], null)).IsValid.ShouldBeTrue();
        validator.Validate(new SetMemberAccessCommand(false, [Guid.Empty], null, null)).IsValid.ShouldBeFalse();
        validator.Validate(new SetMemberAccessCommand(false, null, null,
            Enumerable.Range(0, MemberAccessRules.MaxAssignments + 1).Select(_ => Guid.CreateVersion7()).ToList()))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Caller_access_is_empty_lists_with_all_sites()
    {
        var access = CallerAccess.From(SiteScope.Everything, false)!;

        access.AllSites.ShouldBeTrue();
        access.SiteIds.ShouldBeEmpty();
        access.RegionIds.ShouldBeEmpty();
    }

    [Fact]
    public void Caller_access_lists_the_scope_when_limited()
    {
        var access = CallerAccess.From(Limited(), false)!;

        access.AllSites.ShouldBeFalse();
        access.SiteIds.ShouldBe([SiteA]);
        access.RegionIds.ShouldBe([RegionA]);
    }

    [Fact]
    public void Caller_access_is_empty_without_an_active_membership() =>
        CallerAccess.From(SiteScope.Nothing, false)!.SiteIds.ShouldBeEmpty();

    [Fact]
    public void Platform_admins_without_an_organization_have_no_access_object() =>
        CallerAccess.From(SiteScope.Everything, true).ShouldBeNull();
}