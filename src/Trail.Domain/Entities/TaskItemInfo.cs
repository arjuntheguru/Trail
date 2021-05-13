using FluentValidation;
using System;

namespace Trail.Domain.Entities
{
    public class TaskItemInfo
    {   
        public string Name { get; set; }
    }

    public class TaskItemInfoValidator : AbstractValidator<TaskItemInfo>
    {
        public TaskItemInfoValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
        }
    }
}
