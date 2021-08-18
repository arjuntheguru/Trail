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
    public class SiteCrudService
    {
        private readonly ICrudService<Site> _siteCrudService;
        private readonly IMongoCollection<Site> _collection;
        public SiteCrudService(ICrudService<Site> siteCrudService)
        {
            _siteCrudService = siteCrudService;

            var database = _siteCrudService.Database();
            var collection = database.GetCollection<Site>(_siteCrudService.GetCollectionName(typeof(Site)));

            _collection = collection;
        }

        public RecordCount<Site> AsQueryable(PaginationFilter filter, string searchQuery)
        {          

            var result = new RecordCount<Site>
            {
                Records = _collection.AsQueryable()
                .Where(p => p.Name.ToLower().Contains(searchQuery))
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
            };

            result.Count = _collection.AsQueryable()
               .Where(p => p.Name.ToLower().Contains(searchQuery)).Count();

            return result;
        }

        public RecordCount<Site> FilterBy(PaginationFilter filter,
            Expression<Func<Site, bool>> filterExpression, string searchQuery)
        {
            var result = new RecordCount<Site>
            {
               
                Records = _collection.Find(filterExpression).ToEnumerable()
                .Where(p => p.Name.ToLower().Contains(searchQuery))
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)

            };

            result.Count = _collection.Find(filterExpression).ToEnumerable()
                .Where(p => p.Name.ToLower().Contains(searchQuery)).Count();

            return result;
        }
    }
}
