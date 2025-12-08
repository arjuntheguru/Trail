using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trail.Domain.Common;
using Trail.Domain.Enums;

namespace Trail.Domain.Entities
{
    public class TaskListInfo : BaseEntity
    {
        public string Name { get; set; }
        public TaskType TaskType { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public IEnumerable<Tag> Tags { get; set; }
        public IEnumerable<RecordLog> RecordLogs { get; set; }
        public string CompanyId { get; set; }        
        public IEnumerable<TaskItemInfo> TaskItemInfo { get; set; }
        public IEnumerable<Group> Groups { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class TaskListInfoValidator : AbstractValidator<TaskListInfo>
    {
        public TaskListInfoValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Description).NotEmpty();
            RuleFor(p => p.TaskItemInfo)
                .NotNull()
                .Must(p => p.Any())
                .When(p => p.TaskType == TaskType.Checklist)
                .WithMessage("At least one task item is required for checklist tasks");

            RuleForEach(p => p.TaskItemInfo)
                .SetValidator(new TaskItemInfoValidator())
                .When(p => p.TaskType == TaskType.Checklist);

            RuleForEach(p => p.Groups)
                .SetValidator(new GroupValidator())
                .When(p => p.Groups != null && p.Groups.Any());
        }
    }
}
