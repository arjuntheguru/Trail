using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Application.Common.Models.DTO
{
    public class InsertCommentDTO
    {
        public string CalendarId { get; set; }
        public string TaskListId { get; set; }
        public string Description { get; set; }
    }
}
