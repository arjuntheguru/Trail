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
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;

namespace Trail.WebAPI.Controllers
{
    [Authorize]
    public class SiteController : ApiControllerBase
    {
        private readonly ICrudService<Site> _siteCrudService;
        private readonly IUriService _uriService;

        public SiteController(
            ICrudService<Site> siteCrudService,
            IUriService uriService)
        {
            _siteCrudService = siteCrudService;
            _uriService = uriService;
        }

        [Authorize(Roles="Admin")]
        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _siteCrudService.AsQueryable(validFilter);

            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [Authorize]
        [HttpGet("id/{id}")]
        public IActionResult GetById(string id)
        {

            var record = _siteCrudService.FindById(id);

            var response = new Response<Site>(record, "Site fetched successfully");

            return Ok(response);
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("{companyId}")]
        public IActionResult GetSitesFromCompanyId([FromQuery] PaginationFilter filter, string companyId)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _siteCrudService.FilterBy(validFilter, p => p.CompanyId == companyId);

            var requestParameters = new ArrayList
            {
                new RequestParameter { ParameterName = "companyId", ParameterValue = companyId }
            };            

            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, requestParameters.ToArray(typeof(RequestParameter)) as RequestParameter[]);

            return Ok(pagedReponse);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all/{companyId}")]
        public IActionResult GetSitesFromCompanyIdWithoutPagination(string companyId) 
        {           

            var records = _siteCrudService.FilterBy(p => p.CompanyId == companyId);            

            return Ok(records.ToList());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(Site site)
        {
            var id = await _siteCrudService.InsertOneAsync(site);

            var response = new Response<string>(id, "Site created successfully");

            return Ok(response);
        }

        [Authorize(Roles ="Admin,Manager")]
        [HttpPut]
        public async Task<IActionResult> Update(Site site)
        {
            var item = await _siteCrudService.FindByIdAsync(site.Id);

            if (item == null)
            {
                return NotFound(new Response<Site>("Site does not exist"));
            }

            item.Name = site.Name;
            item.Address = site.Address;
            item.BuisnessHours = site.BuisnessHours;
            item.LastSeen = DateTime.Now;

            var response = await _siteCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Site>(response, "Site updated successfully"));
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var item = await _siteCrudService.FindByIdAsync(id);

            if (item == null)
            {
                return NotFound(new Response<Site>("Site does not exist"));
            }

            item.IsActive = false;

            var response = await _siteCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Site>(response, "Site deleted successfully"));

        }
    }
}