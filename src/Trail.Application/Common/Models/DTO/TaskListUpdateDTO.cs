using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Application.Common.Models.DTO
{
    public class TaskListUpdateDTO
    {
        public string CalendarId { get; set; }
        public string TaskListId { get; set; }
        public string ApprovedBy { get; set; }
        
    }
}
