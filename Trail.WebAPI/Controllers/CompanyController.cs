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

namespace Trail.WebAPI.Controllers
{
    //[Authorize(Roles = "SuperAdmin")]
    public class CompanyController : ApiControllerBase
    {
        private readonly ICrudService<Company> _companyCrudService;
        private readonly IUriService _uriService;

        public CompanyController(
            ICrudService<Company> companyCrudService,
            IUriService uriService)
        {
            _companyCrudService = companyCrudService;
            _uriService = uriService;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _companyCrudService.AsQueryable(validFilter);

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

            return Ok(new Response<string>(response, "Company updated successfully"));

        }

    }
}
