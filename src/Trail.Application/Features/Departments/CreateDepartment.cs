using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Departments;

public sealed record CreateDepartmentCommand(string Name);

internal sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(Department.NameMaxLength);
}

public sealed class CreateDepartmentHandler(IAppDbContext db)
{
    public async Task<Result<DepartmentResponse>> Handle(CreateDepartmentCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name, ct))
            return Error.Conflict("department.name_taken", "A department with this name already exists.");

        var department = new Department { Name = name };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return new DepartmentResponse(department.Id, department.Name);
    }
}
