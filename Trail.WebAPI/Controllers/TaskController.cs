﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;

namespace Trail.WebAPI.Controllers
{
    [Authorize]
    public class TaskController : ApiControllerBase
    {
        private readonly ICrudService<TaskListInfo> _taskListInfoCrudService;
        private readonly ICrudService<User> _userCrudService;
        private readonly ICrudService<TaskAllocation> _taskAllocationCrudService;
        private readonly IUriService _uriService;

        public TaskController(
            ICrudService<TaskListInfo> taskListCrudService,
            ICrudService<TaskAllocation> taskAllocationCrudService,
            ICrudService<User> userCrudService,
            IUriService uriService)
        {
            _taskListInfoCrudService = taskListCrudService;
            _taskAllocationCrudService = taskAllocationCrudService;
            _userCrudService = userCrudService;
            _uriService = uriService;
        }
        
        [Authorize(Roles ="SuperAdmin")]
        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _taskListInfoCrudService.FilterBy(validFilter, p => p.CompanyId == null);

            var pagedReponse = PaginationHelper.CreatePagedReponse<TaskListInfo>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("{companyId}")]
        public IActionResult GetTaskFromCompanyId([FromQuery] PaginationFilter filter, string companyId)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _taskListInfoCrudService.FilterBy(validFilter, p => p.CompanyId == companyId);

            var pagedReponse = PaginationHelper.CreatePagedReponse<TaskListInfo>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        //[HttpGet("{id}")]
        //public IActionResult GetById(string id)
        //{
        //    var record = _taskListInfoCrudService.FindById(id);

        //    if (record == null)
        //    {
        //        return NotFound(new Response<TaskListInfo>("Site does not exist"));
        //    }

        //    var response = new Response<TaskListInfo>(record, "Task List fetched successfully");

        //    return Ok(response);
        //}

        [HttpPost]
        public async Task<IActionResult> Create(TaskListInfo taskList)
        {

            var id = await _taskListInfoCrudService.InsertOneAsync(taskList);

            var response = new Response<string>(id, "Task List created successfully");

            return Ok(response);
        }

        [HttpPut]
        public async Task<IActionResult> Update(TaskListInfo task)
        {
            var item = await _taskListInfoCrudService.FindByIdAsync(task.Id);

            if (item == null)
            {
                return NotFound(new Response<TaskListInfo>("Task List does not exist"));
            }

            item.Name = task.Name;
            item.Description = task.Description;
            item.TaskItemInfo = task.TaskItemInfo;

            var response = await _taskListInfoCrudService.ReplaceOneAsync(item);

            return Ok(new Response<TaskListInfo>(response, "Task List updated successfully"));
        }

        [HttpGet("unallocated/{siteId}")]
        public async Task<IActionResult> GetUnallocatedTask([FromQuery] PaginationFilter filter, string siteId)
        {
            var userId = User.FindFirstValue("UserName");
            var user = await _userCrudService.FindOneAsync(p => p.UserName == userId);

            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var defaultTasks = _taskListInfoCrudService.FilterBy(p => p.CompanyId == null);
            var customTasks = _taskListInfoCrudService.FilterBy(p => p.CompanyId == user.CompanyId);
            var finalTaskList = defaultTasks.Concat(customTasks);

            var allocatedTasks = _taskAllocationCrudService.FilterBy(p => p.SiteId == siteId);

            var unallocatedTasksCount = finalTaskList.Where(p => !allocatedTasks.Select(s => s.TaskId).Contains(p.Id)).ToList().Count;

            var unallocatedTasks = finalTaskList.Where(p => !allocatedTasks.Select(s => s.TaskId).Contains(p.Id))
                 .Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                .Take(validFilter.PageSize);

            var pagedReponse = PaginationHelper.CreatePagedReponse<TaskListInfo>(unallocatedTasks.ToList(), validFilter, unallocatedTasksCount, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);

        }

        [HttpGet("allocation/{id}")]
        public IActionResult GetTaskAllocationById(string id)
        {
            var record = _taskAllocationCrudService.FindById(id);

            if (record == null)
            {
                return NotFound(new Response<TaskAllocation>($"Invalid Id {id}"));
            }

            var response = new Response<TaskAllocation>(record, "Task Allocation fetched successfully");

            return Ok(response);
        }

        [HttpGet("allocation/site/{siteId}")]
        public IActionResult GetTaskAllocationBySiteId([FromQuery] PaginationFilter filter, string siteId)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _taskAllocationCrudService.FilterBy(validFilter, p => p.SiteId == siteId);

            var pagedReponse = PaginationHelper.CreatePagedReponse<TaskAllocation>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [HttpPost("allocation")]
        public async Task<IActionResult> CreateTaskAllocation(TaskAllocation taskAllocation)
        {
            var id = await _taskAllocationCrudService.InsertOneAsync(taskAllocation);

            var response = new Response<string>(id, "Task Allocation created successfully");

            return Ok(response);
        }

        [HttpPut("allocation")]
        public async Task<IActionResult> UpdateTaskAllocation(TaskAllocation taskAllocation)
        {
            var item = await _taskAllocationCrudService.FindByIdAsync(taskAllocation.Id);

            if (item == null)
            {
                return NotFound(new Response<TaskAllocation>("Task Allocation does not exist"));
            }

            item.TaskFrequency = taskAllocation.TaskFrequency;
            item.WeeklyRepetition = taskAllocation.WeeklyRepetition;

            var response = await _taskAllocationCrudService.ReplaceOneAsync(item);

            return Ok(new Response<TaskAllocation>(response, "Task Allocation updated successfully"));
        }

        [HttpDelete("allocation/{id}")]
        public async Task<IActionResult> DeleteTaskAllocation(string id)
        {
            await _taskAllocationCrudService.DeleteOneAsync(p => p.Id == id);

            var response = new Response<string>(id, "Task Allocation deleted successfully");

            return Ok(response);
        }

    }
}