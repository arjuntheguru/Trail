using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Organizations;

public sealed record UpdateCurrentOrganizationCommand(string Name, string DefaultTimeZone);

internal sealed class UpdateCurrentOrganizationValidator : AbstractValidator<UpdateCurrentOrganizationCommand>
{
    public UpdateCurrentOrganizationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Organization.NameMaxLength);
        RuleFor(x => x.DefaultTimeZone).NotEmpty().Must(TimeZones.IsValidIana)
            .WithMessage("Time zone must be a valid IANA id such as Europe/London.");
    }
}

public sealed class UpdateCurrentOrganizationHandler(IAppDbContext db, ICurrentUser user)
{
    public async Task<Result<OrganizationResponse>> Handle(UpdateCurrentOrganizationCommand command, CancellationToken ct)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct);
        if (organization is null) return Error.NotFound("organization.not_found", "Organization not found.");

        organization.Name = command.Name.Trim();
        organization.DefaultTimeZone = command.DefaultTimeZone;
        await db.SaveChangesAsync(ct);
        return new OrganizationResponse(organization.Id, organization.Name, organization.Slug, organization.DefaultTimeZone);
    }
}
