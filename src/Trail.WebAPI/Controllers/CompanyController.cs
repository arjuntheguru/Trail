using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;
using Trail.Infrastructure.Services;

namespace Trail.WebAPI.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class CompanyController : ApiControllerBase
    {
        private readonly ICrudService<Company> _companyCrudService;
        private readonly CompanyCrudService _companySearchService;
        private readonly IUriService _uriService;

        public CompanyController(
            ICrudService<Company> companyCrudService,
            IUriService uriService,
            CompanyCrudService companySearchService)
        {
            _companyCrudService = companyCrudService;
            _uriService = uriService;
            _companySearchService = companySearchService;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter, string searchQuery = null)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = new RecordCount<Company>();
            
            if(String.IsNullOrWhiteSpace(searchQuery))
            {
                records = _companyCrudService.AsQueryable(validFilter);

            }
            else
            {
                records = _companySearchService.AsQueryable(validFilter, searchQuery.ToLower());
            }

            var pagedReponse = PaginationHelper.CreatePagedReponse<Company>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(string id)
        {

            var record = _companyCrudService.FindById(id);

            var response = new Response<Company>(record, "Company fetched successfully");

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Company company)
        {
            var id = await _companyCrudService.InsertOneAsync(company);

            var response = new Response<string>(id, "Company created successfully");

            return Ok(response);
        }

        [HttpPut]
        public async Task<IActionResult> Update(Company company)
        {
            var item = await _companyCrudService.FindByIdAsync(company.Id);

            if (item == null)
            {
                return NotFound(new Response<Company>("Company does not exist"));
            }

            item.Email = company.Email;
            item.Address = company.Address;

            var response = await _companyCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Company>(response, "Company updated successfully"));

        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var item = await _companyCrudService.FindByIdAsync(id);

            if (item == null)
            {
                return NotFound(new Response<Company>("Company does not exist"));
            }

            item.IsActive = false;

            var response = await _companyCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Company>(response, "Company deleted successfully"));

        }

    }
}
