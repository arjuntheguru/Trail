using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Application.Common.Wrappers;
using Trail.Application.Common.Models.DTO;
using Trail.Domain.Common;
using Trail.Domain.Entities;
using Trail.Infrastructure.Services;

namespace Trail.WebAPI.Controllers
{
    [Authorize]
    public class CompanyController : ApiControllerBase
    {
        private readonly ICrudService<Company> _companyCrudService;
        private readonly ICrudService<User> _userCrudService;
        private readonly CompanyCrudService _companySearchService;
        private readonly IUriService _uriService;

        public CompanyController(
            ICrudService<Company> companyCrudService,
            ICrudService<User> userCrudService,
            IUriService uriService,
            CompanyCrudService companySearchService)
        {
            _companyCrudService = companyCrudService;
            _userCrudService = userCrudService;
            _uriService = uriService;
            _companySearchService = companySearchService;
        }

        [Authorize(Roles = "SuperAdmin")]
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

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("{id}")]
        public IActionResult GetById(string id)
        {

            var record = _companyCrudService.FindById(id);

            var response = new Response<Company>(record, "Company fetched successfully");

            return Ok(response);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create(Company company)
        {
            var id = await _companyCrudService.InsertOneAsync(company);

            var response = new Response<string>(id, "Company created successfully");

            return Ok(response);
        }

        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPut]
        public async Task<IActionResult> Update(Company company)
        {
            var item = await _companyCrudService.FindByIdAsync(company.Id);

            if (item == null)
            {
                return NotFound(new Response<Company>("Company does not exist"));
            }

            if (User.IsInRole(Role.Admin))
            {
                var userName = User.FindFirstValue("UserName");
                var user = await _userCrudService.FindOneAsync(p => p.UserName == userName);
                if (user == null || user.CompanyId != company.Id)
                {
                    return Forbid();
                }
            }

            item.Name = company.Name;
            item.RegistrationNo = company.RegistrationNo;
            item.Email = company.Email;
            item.Address = company.Address;

            var response = await _companyCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Company>(response, "Company updated successfully"));

        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> Delete(string id, [FromBody] CompanyStatusUpdateDTO statusUpdate)
        {
            var item = await _companyCrudService.FindByIdAsync(id);

            if (item == null)
            {
                return NotFound(new Response<Company>("Company does not exist"));
            }

            item.IsActive = statusUpdate?.IsActive ?? false;

            var response = await _companyCrudService.ReplaceOneAsync(item);

            var message = item.IsActive ? "Company activated successfully" : "Company deactivated successfully";

            return Ok(new Response<Company>(response, message));

        }

    }
}
