using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;

namespace Trail.Infrastructure.Services
{
    public class UserCrudService
    {
        private readonly ICrudService<User> _userCrudService;
        public UserCrudService(ICrudService<User> userCrudService)
        {
            _userCrudService = userCrudService;
        }

        public RecordCount<User> FilterBy(PaginationFilter filter, Expression<Func<User, bool>> filterExpression, string searchQuery = null)
        {
            var database = _userCrudService.Database();
            var collection = database.GetCollection<User>(_userCrudService.GetCollectionName(typeof(User)));

            var records = collection.Find(filterExpression).ToEnumerable()
                .Where(p => p.FirstName.ToLower().Contains(searchQuery)
                || p.LastName.ToLower().Contains(searchQuery)
                || p.Email.ToLower().Contains(searchQuery)
                || p.UserName.ToLower().Contains(searchQuery)
                || p.Role.ToLower().Contains(searchQuery));

            var result = new RecordCount<User>
            {
                Records = records
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
            };

            result.Count = records.Count();

            return result;
        }
    }
}
