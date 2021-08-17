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
    public class CompanyCrudService
    {
        private readonly ICrudService<Company> _companyCrudService;            
        public CompanyCrudService(ICrudService<Company> companyCrudService)
        {
            _companyCrudService = companyCrudService;
        }

        public  RecordCount<Company> AsQueryable(PaginationFilter filter, string searchQuery)
        {
            var database = _companyCrudService.Database();
            var collection = database.GetCollection<Company>(_companyCrudService.GetCollectionName(typeof(Company)));

            var result = new RecordCount<Company>
            {
                Records = collection.AsQueryable()
                .Where(p => p.Name.ToLower().Contains(searchQuery) || p.RegistrationNo.ToLower().Contains(searchQuery) || p.Email.ToLower().Contains(searchQuery))
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)               
            };

            result.Count = collection.AsQueryable()
                .Where(p => p.Name.ToLower().Contains(searchQuery) || p.RegistrationNo.ToLower().Contains(searchQuery) || p.Email.ToLower().Contains(searchQuery)).Count();

            return result;
        }
    }
}
