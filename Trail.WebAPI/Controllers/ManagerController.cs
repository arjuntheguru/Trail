using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;

namespace Trail.WebAPI.Controllers
{
    public class TaskManager 
    {
        private readonly ICrudService<TaskAllocation> _taskAllocationCrudService;
        private readonly ICrudService<Calendar> _calendarCrudService;
        private readonly ICrudService<TaskListInfo> _taskListInfoCrudService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public TaskManager(
            ICrudService<TaskAllocation> taskAllocationCrudService,
            ICrudService<Calendar> calendarCrudService,
            ICrudService<TaskListInfo> taskListCrudService,
            IBackgroundJobClient backgroundJobClient)
        {
            _taskAllocationCrudService = taskAllocationCrudService;
            _calendarCrudService = calendarCrudService;
            _taskListInfoCrudService = taskListCrudService;
            _backgroundJobClient = backgroundJobClient;
        }

        public void AssignTasks()
        {
            
        }
    }    
}
