using FluentValidation;
using System;
using System.Collections.Generic;
using Trail.Domain.Common;
using Trail.Domain.Enums;

namespace Trail.Domain.Entities
{
    public class TaskList 
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public IEnumerable<TaskItem> TaskItems { get; set; }
        public int Score { get; set; }        
        public DateTime DueBy { get; set; }
        public string ApprovedBy { get; set; }
    }

    public class TaskListValidator : AbstractValidator<TaskList>
    {
        public TaskListValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Description).NotEmpty();
            RuleForEach(p => p.TaskItems).SetValidator(new TaskItemValidator());
        }
    }
}