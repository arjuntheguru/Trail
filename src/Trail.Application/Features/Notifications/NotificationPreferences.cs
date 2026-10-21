using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

public sealed record UpdateNotificationPreferencesCommand(IReadOnlyList<NotificationKind> EmailKinds, bool DigestEnabled, int DigestHour);

internal sealed class UpdateNotificationPreferencesValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesValidator()
    {
        RuleFor(x => x.EmailKinds).NotNull();
        RuleForEach(x => x.EmailKinds).IsInEnum();
        RuleFor(x => x.DigestHour).InclusiveBetween(0, 23);
    }
}

public sealed class NotificationPreferencesHandler(IAppDbContext db, ICurrentUser caller)
{
    private static readonly Error NoMembership = Error.Forbidden("notification.no_membership", "You are not acting inside an organization.");

    public async Task<Result<NotificationPreferencesResponse>> Get(CancellationToken ct)
    {
        if (caller.MembershipId is not { } membershipId) return NoMembership;
        var preference = await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.MembershipId == membershipId, ct);
        return ToResponse(preference ?? new NotificationPreference { MembershipId = membershipId });
    }

    public async Task<Result<NotificationPreferencesResponse>> Update(UpdateNotificationPreferencesCommand command, CancellationToken ct)
    {
        if (caller.MembershipId is not { } membershipId) return NoMembership;

        var preference = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.MembershipId == membershipId, ct);
        if (preference is null)
        {
            preference = new NotificationPreference { MembershipId = membershipId };
            db.NotificationPreferences.Add(preference);
        }

        preference.EmailKinds = [.. command.EmailKinds.Distinct()];
        preference.DigestEnabled = command.DigestEnabled;
        preference.DigestHour = command.DigestHour;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // A parallel request created the row first (e.g. a double click); the caller can simply retry.
            return Error.Conflict("notification.preferences_busy", "Your preferences were just changed. Try again.");
        }
        return ToResponse(preference);
    }

    private static NotificationPreferencesResponse ToResponse(NotificationPreference preference) =>
        new([.. preference.EmailKinds.OrderBy(k => k)], preference.DigestEnabled, preference.DigestHour);
}
