using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
using Trail.WebAPI.Helpers;
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
        private readonly ICrudService<TaskListInfo> _taskListInfoCrudService;
        private readonly IUriService _uriService;
        private readonly IWebHostEnvironment _environment;

        public CalendarController(
            ICrudService<Calendar> calendarCrudService,
            ICrudService<Site> siteCrudService,
            ICrudService<TaskListInfo> taskListInfoCrudService,
            IUriService uriService,
            IWebHostEnvironment environment)
        {
            _calendarCrudService = calendarCrudService;
            _siteCrudService = siteCrudService;
            _taskListInfoCrudService = taskListInfoCrudService;
            _uriService = uriService;
            _environment = environment;
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
            // Normalize the date to midnight (start of day) to compare only the date portion, ignoring time
            var normalizedDate = date.Date;
            var nextDay = normalizedDate.AddDays(1);

            // Compare dates by checking if the calendar date falls within the same day
            var record = await _calendarCrudService.FindOneAsync(p => 
                p.SiteId == siteId && 
                p.Date >= normalizedDate && 
                p.Date < nextDay);

            if (record == null)
            {
                return Ok(new Response<Object>(new { }, $"Fetched calendar for {normalizedDate:yyyy-MM-dd} successfully"));
            }

            // Enrich TaskList items with groups from TaskListInfo if groups are missing
            if (record.TaskList != null)
            {
                var taskList = record.TaskList.ToList();
                foreach (var taskListItem in taskList)
                {
                    // If groups are null or empty, try to populate them from TaskListInfo
                    if (taskListItem.Groups == null || !taskListItem.Groups.Any())
                    {
                        // Find matching TaskListInfo by name and description
                        var matchingTask = await _taskListInfoCrudService.FindOneAsync(t => 
                            t.Name == taskListItem.Name && 
                            t.Description == taskListItem.Description &&
                            t.IsActive);

                        if (matchingTask != null && matchingTask.Groups != null && matchingTask.Groups.Any())
                        {
                            // Copy groups from TaskListInfo, initializing answers as empty
                            var groups = new List<Group>();
                            foreach (var group in matchingTask.Groups)
                            {
                                var questionAnswerPairs = new List<QuestionAnswerPair>();
                                foreach (var qaPair in group.QuestionAnswerPairs)
                                {
                                    questionAnswerPairs.Add(new QuestionAnswerPair
                                    {
                                        Question = qaPair.Question,
                                        Answer = string.Empty // Initialize answer as empty
                                    });
                                }
                                groups.Add(new Group
                                {
                                    GroupName = group.GroupName,
                                    QuestionAnswerPairs = questionAnswerPairs
                                });
                            }
                            taskListItem.Groups = groups;
                        }
                    }
                }
                record.TaskList = taskList;
            }

            var response = new Response<Calendar>(record, $"Fetched calendar for {normalizedDate:yyyy-MM-dd} successfully");

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

        [HttpPatch("updateGroups")]
        public async Task<IActionResult> UpdateGroups(UpdateGroupsDTO updateGroupsDTO)
        {
            var item = await _calendarCrudService.FindByIdAsync(updateGroupsDTO.CalendarId);

            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            var taskList = item.TaskList.ToList();
            var taskListInfo = taskList.Single(p => p.Id == updateGroupsDTO.TaskListId);

            // Update Groups with answers
            if (updateGroupsDTO.Groups != null && updateGroupsDTO.Groups.Any())
            {
                // Completely replace the groups on the task instance with what the client sends,
                // and drop any groups that don't contain at least one question.
                var updatedGroups = new List<Group>();

                foreach (var groupUpdate in updateGroupsDTO.Groups)
                {
                    if (groupUpdate.QuestionAnswerPairs == null)
                    {
                        continue;
                    }

                    var qaPairs = groupUpdate.QuestionAnswerPairs
                        .Where(qa => !string.IsNullOrWhiteSpace(qa.Question))
                        .Select(qa => new QuestionAnswerPair
                        {
                            Question = qa.Question,
                            Answer = qa.Answer ?? string.Empty
                        })
                        .ToList();

                    // Skip groups that end up with no valid questions
                    if (!qaPairs.Any())
                    {
                        continue;
                    }

                    updatedGroups.Add(new Group
                    {
                        GroupName = groupUpdate.GroupName,
                        QuestionAnswerPairs = qaPairs
                    });
                }

                taskListInfo.Groups = updatedGroups;
            }

            taskList[taskList.FindIndex(p => p.Id == updateGroupsDTO.TaskListId)] = taskListInfo;
            item.TaskList = taskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Groups updated successfully"));
        }

        [HttpPut("comment")]
        public async Task<IActionResult> InsertComment([FromForm] InsertCommentDTO insertComment)
        {
            var item = await _calendarCrudService.FindByIdAsync(insertComment.CalendarId);
            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            // Upload images if any
            var uploadedFilePaths = new List<string>();
            if (Request.Form.Files != null && Request.Form.Files.Count > 0)
            {
                uploadedFilePaths = await FileUploadHelper.UploadImagesAsync(Request.Form.Files, _environment);
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                Description = insertComment.Description,
                Files = uploadedFilePaths.ToArray(),
                CreatedBy = User.FindFirstValue("UserName"),
                CreatedDate = DateTime.Now
            };

            var taskList = item.TaskList.ToList();
            var taskListInfo = taskList.Single(p => p.Id == insertComment.TaskListId);

            var comments = taskListInfo.Comments.Append(comment);
            taskListInfo.Comments = comments;

            taskList[taskList.FindIndex(p => p.Id == insertComment.TaskListId)] = taskListInfo;

            item.TaskList = taskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Calendar>(response, "Comment inserted successfully"));

        }
        [Authorize(Roles = "Admin,Manager")]
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
