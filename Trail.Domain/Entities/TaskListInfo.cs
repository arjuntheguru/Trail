using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class TaskListInfo : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IEnumerable<TaskItemInfo> TaskItemInfo { get; set; }
    }

    public class TaskListInfoValidator : AbstractValidator<TaskListInfo>
    {
        public TaskListInfoValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Description).NotEmpty();
            RuleForEach(p => p.TaskItemInfo).SetValidator(new TaskItemInfoValidator());
        }
    }
}
