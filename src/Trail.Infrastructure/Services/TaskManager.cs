using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trail.Application.Common.Interfaces;
using Trail.Domain.Entities;

namespace Trail.Infrastructure.Services
{
    public class TaskManager : ITaskManager
    {
        private readonly ICrudService<TaskAllocation> _taskAllocationCrudService;
        private readonly ICrudService<Calendar> _calendarCrudService;
        private readonly ICrudService<TaskListInfo> _taskListInfoCrudService;

        public TaskManager(
            ICrudService<TaskAllocation> taskAllocationCrudService,
            ICrudService<Calendar> calendarCrudService,
            ICrudService<TaskListInfo> taskListCrudService)
        {
            _taskAllocationCrudService = taskAllocationCrudService;
            _calendarCrudService = calendarCrudService;
            _taskListInfoCrudService = taskListCrudService;
        }

        public void RunTasks()
        {
            // Scheduled jobs still run but will skip if calendar entries already exist for today
            // Immediate creation is now handled in TaskController.CreateTaskAllocation
            // These jobs serve as a backup and will handle future dates
            RecurringJob.AddOrUpdate(() => AssignDailyTasks(), Cron.Daily, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => AssignWeeklyTasks(), Cron.Daily, TimeZoneInfo.Local);
        }

        /// <summary>
        /// Creates a calendar entry for a task allocation on a specific date.
        /// This method is called immediately when a task is assigned (if applicable) and by scheduled jobs.
        /// </summary>
        public async Task CreateCalendarEntryForTaskAllocation(TaskAllocation taskAllocation, DateTime targetDate)
        {
            var task = await _taskListInfoCrudService.FindByIdAsync(taskAllocation.TaskId);
            if (task == null)
            {
                return; // Task not found, skip
            }

            var taskItems = new List<TaskItem>();

            foreach (var taskItem in task.TaskItemInfo)
            {
                taskItems.Add(new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = taskItem.Name,
                    Status = Domain.Enums.TaskStatus.Incomplete
                });
            }

            // Copy Groups from TaskListInfo to TaskList (initialize answers as empty)
            var groups = new List<Group>();
            if (task.Groups != null)
            {
                foreach (var group in task.Groups)
                {
                    var questionAnswerPairs = new List<QuestionAnswerPair>();
                    foreach (var qaPair in group.QuestionAnswerPairs)
                    {
                        questionAnswerPairs.Add(new QuestionAnswerPair
                        {
                            Question = qaPair.Question,
                            Answer = string.Empty // Initialize answer as empty for team user to fill
                        });
                    }
                    groups.Add(new Group
                    {
                        GroupName = group.GroupName,
                        QuestionAnswerPairs = questionAnswerPairs
                    });
                }
            }

            var taskListItem = new TaskList
            {
                Id = Guid.NewGuid().ToString(),
                Name = task.Name,
                Description = task.Description,
                Status = Domain.Enums.TaskStatus.Incomplete,
                TaskItems = taskItems,
                Groups = groups,
                DueBy = new DateTime(
                    targetDate.Year,
                    targetDate.Month,
                    targetDate.Day,
                    taskAllocation.DueBy.Hours,
                    taskAllocation.DueBy.Minutes,
                    taskAllocation.DueBy.Seconds),
                Comments = new List<Comment>()
            };

            // Check if calendar entry already exists for this site and date
            var normalizedDate = targetDate.Date;
            var nextDay = normalizedDate.AddDays(1);
            var calendar = await _calendarCrudService.FindOneAsync(p =>
                p.SiteId == taskAllocation.SiteId &&
                p.Date >= normalizedDate &&
                p.Date < nextDay);

            if (calendar == null)
            {
                // Create new calendar entry
                var taskList = new List<TaskList>();
                taskList.Add(taskListItem);

                var calendarItem = new Calendar
                {
                    SiteId = taskAllocation.SiteId,
                    Date = normalizedDate,
                    TaskList = taskList
                };

                await _calendarCrudService.InsertOneAsync(calendarItem);
            }
            else
            {
                // Check if this task is already in the calendar (prevent duplicates)
                var taskList = calendar.TaskList.ToList();
                var taskExists = taskList.Any(t => t.Name == task.Name && 
                    t.DueBy.Date == normalizedDate && 
                    t.DueBy.TimeOfDay == taskAllocation.DueBy);

                if (!taskExists)
                {
                    taskList.Add(taskListItem);
                    calendar.TaskList = taskList;
                    await _calendarCrudService.ReplaceOneAsync(calendar);
                }
            }
        }

        public async Task AssignDailyTasks()
        {
            // This method is used by scheduled jobs - immediate creation is handled in TaskController
            // It now uses the reusable CreateCalendarEntryForTaskAllocation method which will skip if entry already exists
            var taskAllocations = _taskAllocationCrudService.FilterBy(p => p.TaskFrequency == Domain.Enums.TaskFrequency.Daily);

            foreach (var taskAllocation in taskAllocations)
            {
                // Use the reusable method - it will skip if entry already exists
                await CreateCalendarEntryForTaskAllocation(taskAllocation, DateTime.Today);
            }
        }

        public async Task AssignWeeklyTasks()
        {
            // This method is used by scheduled jobs - immediate creation is handled in TaskController
            // It now uses the reusable CreateCalendarEntryForTaskAllocation method which will skip if entry already exists
            var taskAllocations = _taskAllocationCrudService.FilterBy(p => p.TaskFrequency == Domain.Enums.TaskFrequency.Weekly);

            foreach (var taskAllocation in taskAllocations)
            {
                if (DateTime.Today.DayOfWeek == taskAllocation.WeeklyRepetition)
                {
                    // Use the reusable method - it will skip if entry already exists
                    await CreateCalendarEntryForTaskAllocation(taskAllocation, DateTime.Today);
                }
            }
        }
    }
}