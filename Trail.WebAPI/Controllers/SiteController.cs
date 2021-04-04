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

        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _siteCrudService.AsQueryable(validFilter);

            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

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

        [HttpPost]
        public async Task<IActionResult> Create(Site site)
        {
            var id = await _siteCrudService.InsertOneAsync(site);

            var response = new Response<string>(id, "Site created successfully");

            return Ok(response);
        }

        [HttpPut]
        public async Task<IActionResult> Update(Site site)
        {
            var item = await _siteCrudService.FindByIdAsync(site.Id);

            if (item == null)
            {
                return NotFound(new Response<Company>("Site does not exist"));
            }

            item.Name = site.Name;           
            item.BuisnessHours = site.BuisnessHours;

            var response = await _siteCrudService.ReplaceOneAsync(item);

            return Ok(new Response<string>(response, "Site updated successfully"));
        }
    }
}