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
            RecurringJob.AddOrUpdate(() => AssignDailyTasks(), Cron.Daily, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => AssignWeeklyTasks(), Cron.Daily, TimeZoneInfo.Local);
        }

        public async Task AssignDailyTasks()
        {
            var taskAllocations = _taskAllocationCrudService.FilterBy(p => p.TaskFrequency == Domain.Enums.TaskFrequency.Daily);

            foreach (var taskAllocation in taskAllocations)
            {
                var task = await _taskListInfoCrudService.FindByIdAsync(taskAllocation.TaskId);

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

                var taskListItem = new TaskList
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = task.Name,
                    Description = task.Description,
                    Status = Domain.Enums.TaskStatus.Incomplete,
                    TaskItems = taskItems,
                    DueBy = new DateTime(
                        DateTime.Today.Year,
                        DateTime.Today.Month,
                        DateTime.Today.Day,
                        taskAllocation.DueBy.Hours,
                        taskAllocation.DueBy.Minutes,
                        taskAllocation.DueBy.Seconds),
                    Comments = new List<Comment>()
                };

                var calendar = await _calendarCrudService.FindOneAsync(p => p.SiteId == taskAllocation.SiteId && p.Date == DateTime.Today);

                if (calendar == null)
                {
                    var taskList = new List<TaskList>();
                    taskList.Add(taskListItem);

                    var calendarItem = new Calendar
                    {
                        SiteId = taskAllocation.SiteId,
                        Date = DateTime.Today,
                        TaskList = taskList
                    };

                    await _calendarCrudService.InsertOneAsync(calendarItem);
                }
                else
                {
                    var taskList = calendar.TaskList.ToList();
                    taskList.Add(taskListItem);
                    calendar.TaskList = taskList;
                    await _calendarCrudService.ReplaceOneAsync(calendar);
                }
            }
        }

        public async Task AssignWeeklyTasks()
        {
            var taskAllocations = _taskAllocationCrudService.FilterBy(p => p.TaskFrequency == Domain.Enums.TaskFrequency.Weekly);

            foreach (var taskAllocation in taskAllocations)
            {
                if (DateTime.Today.DayOfWeek == taskAllocation.WeeklyRepetition)
                {
                    var task = await _taskListInfoCrudService.FindByIdAsync(taskAllocation.TaskId);

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

                    var taskListItem = new TaskList
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = task.Name,
                        Description = task.Description,
                        Status = Domain.Enums.TaskStatus.Incomplete,
                        TaskItems = taskItems,
                        DueBy = new DateTime(
                            DateTime.Today.Year,
                            DateTime.Today.Month,
                            DateTime.Today.Day,
                            taskAllocation.DueBy.Hours,
                            taskAllocation.DueBy.Minutes,
                            taskAllocation.DueBy.Seconds),
                        Comments = new List<Comment>()
                };

                    var calendar = await _calendarCrudService.FindOneAsync(p => p.SiteId == taskAllocation.SiteId && p.Date == DateTime.Today);

                    if (calendar == null)
                    {
                        var taskList = new List<TaskList>();
                        taskList.Add(taskListItem);

                        var calendarItem = new Calendar
                        {
                            SiteId = taskAllocation.SiteId,
                            Date = DateTime.Today,
                            TaskList = taskList
                        };

                        await _calendarCrudService.InsertOneAsync(calendarItem);
                    }
                    else
                    {
                        var taskList = calendar.TaskList.ToList();
                        taskList.Add(taskListItem);
                        calendar.TaskList = taskList;
                        await _calendarCrudService.ReplaceOneAsync(calendar);
                    }
                }
            }
        }
    }
}