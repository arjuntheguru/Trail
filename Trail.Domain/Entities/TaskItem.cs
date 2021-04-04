using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;
using Trail.Domain.Enums;

namespace Trail.Domain.Entities
{
    public class TaskItem : TaskItemInfo
    {
        public TaskStatus Status { get; set; }
        public DateTime CompletedTime { get; set; }
        public string CompletedBy { get; set; }
    }

    public class TaskItemValidator : AbstractValidator<TaskItem> 
    {
        public TaskItemValidator()
        {            
            RuleFor(p => p.Status).IsInEnum();
        }
    }
}
