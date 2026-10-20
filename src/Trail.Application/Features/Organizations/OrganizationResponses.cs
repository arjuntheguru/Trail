namespace Trail.Application.Features.Organizations;

public sealed record OrganizationResponse(Guid Id, string Name, string Slug, string DefaultTimeZone);
