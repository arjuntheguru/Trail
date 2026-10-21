using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Departments;

public sealed record UpdateDepartmentCommand(string Name);

internal sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(Department.NameMaxLength);
}

public sealed class UpdateDepartmentHandler(IAppDbContext db)
{
    public async Task<Result<DepartmentResponse>> Handle(Guid id, UpdateDepartmentCommand command, CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null) return Error.NotFound("department.not_found", "Department not found.");

        var name = command.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct))
            return Error.Conflict("department.name_taken", "A department with this name already exists.");

        department.Name = name;
        await db.SaveChangesAsync(ct);
        return new DepartmentResponse(department.Id, department.Name);
    }
}
