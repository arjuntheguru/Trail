using MongoDB.Driver;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
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

            // Convert expression to MongoDB filter
            var baseFilter = Builders<User>.Filter.Where(filterExpression);
            var finalFilter = baseFilter;

            // Add search filter if provided
            if (!string.IsNullOrEmpty(searchQuery))
            {
                var searchTerm = searchQuery.ToLower();
                var tokens = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Build an OR filter for a single token representing a match against the common fields.
                FilterDefinition<User> BuildTokenFilter(string term) =>
                    Builders<User>.Filter.Or(
                        Builders<User>.Filter.Where(u => u.FirstName != null && u.FirstName.ToLower().Contains(term)),
                        Builders<User>.Filter.Where(u => u.LastName != null && u.LastName.ToLower().Contains(term)),
                        Builders<User>.Filter.Where(u => u.Email != null && u.Email.ToLower().Contains(term)),
                        Builders<User>.Filter.Where(u => u.UserName != null && u.UserName.ToLower().Contains(term)),
                        Builders<User>.Filter.Where(u => u.Role != null && u.Role.ToLower().Contains(term))
                    );

                FilterDefinition<User> searchFilter;

                if (tokens.Length <= 1)
                {
                    // Single token behaves the same as the previous implementation.
                    searchFilter = BuildTokenFilter(searchTerm);
                }
                else
                {
                    // Require every token (e.g. first + last name) to match at least one searchable field.
                    var tokenFilters = tokens
                        .Select(token => BuildTokenFilter(token))
                        .ToArray();

                    searchFilter = Builders<User>.Filter.And(tokenFilters);
                }

                finalFilter = Builders<User>.Filter.And(baseFilter, searchFilter);
            }

            // Get total count
            var totalCount = (int)collection.CountDocuments(finalFilter);

            // Get paginated results
            var records = collection.Find(finalFilter)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToList();

            return new RecordCount<User>
            {
                Records = records,
                Count = totalCount
            };
        }

        public async Task<User> FindOneAsync(Expression<Func<User, bool>> filterExpression)
        {
            var database = _userCrudService.Database();
            var collection = database.GetCollection<User>(_userCrudService.GetCollectionName(typeof(User)));

            return await collection.Find(filterExpression).FirstOrDefaultAsync();
        }
    }
}