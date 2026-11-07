using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;

namespace Trail.Application.Features.Departments;

public sealed class ListDepartmentsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<DepartmentResponse>> Handle(CancellationToken ct) =>
        await db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse(d.Id, d.Name))
            .ToListAsync(ct);
}
