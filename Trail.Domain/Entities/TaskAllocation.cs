using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;
using Trail.Domain.Enums;

namespace Trail.Domain.Entities
{
    public class TaskAllocation : BaseEntity
    {
        public string SiteId { get; set; }
        public string TaskId { get; set; }
        public TaskFrequency TaskFrequency { get; set; }
        public DayOfWeek WeeklyRepetition { get; set; }
        public int MonthlyRepetition { get; set; }

    }
}
