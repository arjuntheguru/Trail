using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Application.Common.Models.DTO
{
    public class TaskItemUpdateDTO
    {
        public string CalendarId { get; set; }
        public string  TaskListId { get; set; }
        public string TaskItemId { get; set; }
        public int TaskStatus { get; set; }
    }
}
