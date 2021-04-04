using FluentValidation;
using System;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class BuisnessHour
    {
        public DayOfWeek Day { get; set; }
        public WeekDayTime BeforeOpenFrom { get; set; }
        public WeekDayTime BeforeOpenTo { get; set; }
        public WeekDayTime OpenFrom { get; set; }
        public WeekDayTime OpenTo { get; set; }
        public WeekDayTime MorningFrom { get; set; }
        public WeekDayTime MorningTo { get; set; }
        public WeekDayTime LunchFrom { get; set; }
        public WeekDayTime LunchTo { get; set; }
        public WeekDayTime AfternoonFrom { get; set; }
        public WeekDayTime AfternoonTo { get; set; }
        public WeekDayTime EveningFrom { get; set; }
        public WeekDayTime EveningTo { get; set; }
        public WeekDayTime CloseFrom { get; set; }
        public WeekDayTime CloseTo { get; set; }
        public WeekDayTime AfterCloseFrom { get; set; }
        public WeekDayTime AfterCloseTo { get; set; }
    }

    public class BuisnessHourValidator : AbstractValidator<BuisnessHour>
    {
        public BuisnessHourValidator()
        {
            RuleFor(p => p.Day).IsInEnum();             
        }
    }
}