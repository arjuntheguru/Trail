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
using Trail.Application.Common.Models.DTO;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;
using System.Security.Claims;
using System.Collections.Generic;
using MongoDB.Driver;

namespace Trail.WebAPI.Controllers
{
    [Authorize]
    public class CalendarController : ApiControllerBase
    {
        private readonly ICrudService<Calendar> _calendarCrudService;
        private readonly ICrudService<Site> _siteCrudService;
        private readonly IUriService _uriService;

        public CalendarController(
            ICrudService<Calendar> calendarCrudService,
            ICrudService<Site> siteCrudService,
            IUriService uriService)
        {
            _calendarCrudService = calendarCrudService;
            _siteCrudService = siteCrudService;
            _uriService = uriService;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _calendarCrudService.AsQueryable(validFilter);

            var pagedReponse = PaginationHelper.CreatePagedReponse<Calendar>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [HttpGet("{siteId}")]
        public IActionResult GetCalendarFromSiteId([FromQuery] PaginationFilter filter, string siteId)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _calendarCrudService.FilterBy(validFilter, p => p.SiteId == siteId);

            var requestParameters = new ArrayList
            {
                new RequestParameter { ParameterName = "siteId", ParameterValue = siteId }
            };

            var pagedReponse = PaginationHelper.CreatePagedReponse<Calendar>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, requestParameters.ToArray(typeof(RequestParameter)) as RequestParameter[]);

            return Ok(pagedReponse);
        }

        [HttpGet("{siteId}/{date}")]
        public async Task<IActionResult> GetCalendarFromDate(string siteId, DateTime date)
        {

            var record = await _calendarCrudService.FindOneAsync(p => p.SiteId == siteId && p.Date == date);

            if (record == null)
            {
                return Ok(new Response<Object>(new { }, $"Fetched calendar for {date} successfully"));
            }

            var response = new Response<Calendar>(record, $"Fetched calendar for {date} successfully");

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Calendar calendar)
        {

            var id = await _calendarCrudService.InsertOneAsync(calendar);

            var response = new Response<string>(id, "Calender created successfully");

            return Ok(response);
        }

        [HttpPut]
        public async Task<IActionResult> Update(Calendar calendar)
        {
            var item = await _calendarCrudService.FindByIdAsync(calendar.Id);

            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            item.TaskList = calendar.TaskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Calendar updated successfully"));
        }

        [HttpPatch("updateTaskItemStatus")]
        public async Task<IActionResult> UpdateTaskItemStatus(TaskItemUpdateDTO taskItemUpdate)
        {
            var item = await _calendarCrudService.FindByIdAsync(taskItemUpdate.CalendarId);
            var userId = User.FindFirstValue("UserName");

            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            var taskList = item.TaskList.ToList();
            var taskListInfo = taskList.Single(p => p.Id == taskItemUpdate.TaskListId);
            var taskItem = taskListInfo.TaskItems.ToList();
            var taskItemInfo = taskItem.Single(p => p.Id == taskItemUpdate.TaskItemId);

            taskItemInfo.Status = (Domain.Enums.TaskStatus)taskItemUpdate.TaskStatus;

            if (taskItemInfo.Status == Domain.Enums.TaskStatus.Completed)
            {
                taskItemInfo.CompletedTime = DateTime.Now;
                taskItemInfo.CompletedBy = userId;
            }
            else
            {
                taskItemInfo.CompletedTime = null;
                taskItemInfo.CompletedBy = null;
            }

            taskItem[taskItem.FindIndex(p => p.Id == taskItemUpdate.TaskItemId)] = taskItemInfo;
            taskListInfo.TaskItems = taskItem;

            taskList[taskList.FindIndex(p => p.Id == taskItemUpdate.TaskListId)] = taskListInfo;
            item.TaskList = taskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Task Status updated successfully"));

        }

        [HttpPut("comment")]
        public async Task<IActionResult> InsertComment(InsertCommentDTO insertComment)
        {
            var item = await _calendarCrudService.FindByIdAsync(insertComment.CalendarId);
            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            var filter = Builders<Calendar>.Filter.Eq(p => p.Id, insertComment.CalendarId)
                & Builders<Calendar>.Filter.ElemMatch(p => p.TaskList, Builders<TaskList>.Filter.Eq(p => p.Id, insertComment.TaskListId));

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                Description = insertComment.Description,
                CreatedBy = User.FindFirstValue("UserName"),
                CreatedDate = DateTime.Now
            };

            var update = Builders<Calendar>.Update.Push(p => p.TaskList.ToArray()[-1].Comments, comment);

            var taskList = item.TaskList.ToList();
            var taskListInfo = taskList.Single(p => p.Id == insertComment.TaskListId);

           

            var comments = taskListInfo.Comments.Append(comment);
            taskListInfo.Comments = comments;

            taskList[taskList.FindIndex(p => p.Id == insertComment.TaskListId)] = taskListInfo;

            item.TaskList = taskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Comment inserted successfully"));

        }

        [HttpPut("updateTaskListStatus")]
        public async Task<IActionResult> UpdateTaskListStatus(TaskListUpdateDTO taskListUpdate)
        {
            var item = await _calendarCrudService.FindByIdAsync(taskListUpdate.CalendarId);
            var site = await _siteCrudService.FindByIdAsync(item.SiteId);

            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            var taskList = item.TaskList.ToList();
            var taskListInfo = taskList.Single(p => p.Id == taskListUpdate.TaskListId);          


            taskListInfo.ApprovedBy = taskListUpdate.ApprovedBy;

            taskListInfo.IsApproved = true;
            taskListInfo.ApprovedDate = DateTime.Now;

            if (DateTime.Now > taskListInfo.DueBy.ToLocalTime())
            {
                taskListInfo.Status = Domain.Enums.TaskStatus.LateCompleted;
                taskListInfo.Score = 5;                
            }
            else
            {
                taskListInfo.Status = Domain.Enums.TaskStatus.Completed;
                taskListInfo.Score = 10;
            }

            item.ScoreValue += taskListInfo.Score;
            item.ScoreCount++;

            site.ScoreValue += taskListInfo.Score;
            site.ScoreCount++;

            taskList[taskList.FindIndex(p => p.Id == taskListUpdate.TaskListId)] = taskListInfo;

            item.TaskList = taskList;

            await _siteCrudService.ReplaceOneAsync(site);
            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Task List updated successfully"));
        }
    }
}
