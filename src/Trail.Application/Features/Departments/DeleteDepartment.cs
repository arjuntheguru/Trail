using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Departments;

public sealed class DeleteDepartmentHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null) return Error.NotFound("department.not_found", "Department not found.");

        // Assignments to a deleted department would be dead rows; drop them.
        db.MembershipDepartments.RemoveRange(
            await db.MembershipDepartments.Where(x => x.DepartmentId == id).ToListAsync(ct));
        db.Departments.Remove(department);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
