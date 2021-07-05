using FluentValidation;
using System;
using System.Collections.Generic;
using Trail.Domain.Common;
using Trail.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Trail.Domain.Entities
{
    public class TaskList 
    {
        public string Id { get; set; } 
        public string Name { get; set; }
        public TaskType TaskType { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public IEnumerable<Tag> Tags { get; set; }
        public IEnumerable<RecordLog> RecordLogs { get; set; }
        public int Score { get; set; }
        public DateTime DueBy { get; set; }
        public IEnumerable<Comment> Comments { get; set; }
        public bool IsApproved { get; set; } = false;
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public IEnumerable<TaskItem> TaskItems { get; set; }

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