using System;
using System.Collections.Generic;

namespace Trail.Application.Common.Models.DTO
{
    public class UpdateGroupsDTO
    {
        public string CalendarId { get; set; }
        public string TaskListId { get; set; }
        public IEnumerable<GroupUpdateDTO> Groups { get; set; }
    }

    public class GroupUpdateDTO
    {
        public string GroupName { get; set; }
        public IEnumerable<QuestionAnswerPairUpdateDTO> QuestionAnswerPairs { get; set; }
    }

    public class QuestionAnswerPairUpdateDTO
    {
        public string Question { get; set; }
        public string Answer { get; set; }
    }
}

