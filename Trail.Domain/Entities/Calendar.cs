using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class Calendar : BaseEntity
    {
        public string SiteId { get; set; }
        public DateTime Date { get; set; }
        public IEnumerable<TaskList> TaskList { get; set; }
    }

    public class CalendarValidator : AbstractValidator<Calendar>
    {
        public CalendarValidator()
        {
            RuleFor(p => p.SiteId).NotEmpty();
            RuleFor(p => p.Date).NotEmpty();
        }
    }
}
