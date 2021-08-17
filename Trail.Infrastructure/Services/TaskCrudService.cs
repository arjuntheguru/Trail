using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;

namespace Trail.Infrastructure.Services
{
    public class TaskListInfoCrudService
    {
        private readonly ICrudService<TaskListInfo> _taskCrudService;
        public TaskListInfoCrudService(ICrudService<TaskListInfo> taskCrudService)
        {
            _taskCrudService = taskCrudService;
        }

        public RecordCount<TaskListInfo> AsQueryable(PaginationFilter filter, string searchQuery)
        {
            var database = _taskCrudService.Database();
            var collection = database.GetCollection<TaskListInfo>(_taskCrudService.GetCollectionName(typeof(TaskListInfo)));

            var result = new RecordCount<TaskListInfo>
            {
                Records = collection.AsQueryable()
                .Where(p => p.Name.ToLower().Contains(searchQuery)
                || p.Description.ToLower().Contains(searchQuery))
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
            };

            result.Count = result.Records.Count();

            return result;
        }
    }
}
