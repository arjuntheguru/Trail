using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Common;
using Trail.Domain.Entities;
using Trail.Infrastructure.Services;

namespace Trail.WebAPI.Controllers
{
    [Authorize]
    public class SiteController : ApiControllerBase
    {
        private readonly ICrudService<Site> _siteCrudService;
        private readonly ICrudService<User> _userCrudService;
        private readonly SiteCrudService _siteSearchService;
        private readonly IUriService _uriService;

        public SiteController(
            ICrudService<Site> siteCrudService,
            ICrudService<User> userCrudService,
            IUriService uriService,
            SiteCrudService siteSearchService)
        {
            _siteCrudService = siteCrudService;
            _uriService = uriService;
            _userCrudService = userCrudService;
            _siteSearchService = siteSearchService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _siteCrudService.AsQueryable(validFilter);

            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [HttpGet("id/{id}")]
        public IActionResult GetById(string id)
        {

            var record = _siteCrudService.FindById(id);

            var response = new Response<Site>(record, "Site fetched successfully");

            return Ok(response);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<IActionResult> GetSitesFromCompanyId([FromQuery] PaginationFilter filter, string searchQuery = null, bool? isActive = null)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var userId = User.FindFirstValue("UserName");
            var user = await _userCrudService.FindOneAsync(p => p.UserName == userId);

            var restrictToActiveOnly = User.IsInRole(Role.Team);
            var records = new RecordCount<Site>();

            Expression<Func<Site, bool>> predicate;
            if (restrictToActiveOnly)
            {
                predicate = p => p.CompanyId == user.CompanyId && p.IsActive;
            }
            else if (isActive.HasValue)
            {
                predicate = p => p.CompanyId == user.CompanyId && p.IsActive == isActive.Value;
            }
            else
            {
                predicate = p => p.CompanyId == user.CompanyId;
            }

            if (String.IsNullOrWhiteSpace(searchQuery))
            {
                records = _siteCrudService.FilterBy(validFilter, predicate);
            }
            else
            {
                records = _siteSearchService.FilterBy(validFilter, predicate, searchQuery.ToLower());
            }


            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [Authorize(Roles = "Admin, Manager")]
        [HttpGet("all/{companyId}")]
        public IActionResult GetSitesFromCompanyIdWithoutPagination(string companyId)
        {
            var user = _userCrudService.FindOne(p => p.UserName == User.FindFirstValue("UserName"));
            if (user.CompanyId != companyId)
                throw new UnauthorizedAccessException();
            IEnumerable<Site> records = _siteCrudService.FilterBy(p => p.CompanyId == companyId);
            if (user.Role == "Manager")
            {
                records = records.Where(r => user.SiteId.Contains(r.Id));
            }
            return Ok(records.ToList());
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("manager")]
        public async Task<IActionResult> GetSitesFromCompanyIdManager([FromQuery] PaginationFilter filter, string searchQuery = null, bool? isActive = null)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var userId = User.FindFirstValue("UserName");
            var user = await _userCrudService.FindOneAsync(p => p.UserName == userId);

            var restrictToActiveOnly = User.IsInRole(Role.Team);
            var records = new RecordCount<Site>();
            Expression<Func<Site, bool>> predicate;

            if (restrictToActiveOnly)
            {
                predicate = p => user.SiteId.Contains(p.Id) && p.IsActive;
            }
            else if (isActive.HasValue)
            {
                predicate = p => user.SiteId.Contains(p.Id) && p.IsActive == isActive.Value;
            }
            else
            {
                predicate = p => user.SiteId.Contains(p.Id);
            }

            if (String.IsNullOrWhiteSpace(searchQuery))
            {
                records = _siteCrudService.FilterBy(validFilter, predicate);
            }
            else
            {
                records = _siteSearchService.FilterBy(validFilter, predicate, searchQuery.ToLower());
            }

            var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }




        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(Site site)
        {
            site.IsActive = true;
            var id = await _siteCrudService.InsertOneAsync(site);

            var response = new Response<string>(id, "Site created successfully");

            return Ok(response);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPut]
        public async Task<IActionResult> Update(Site site)
        {
            var item = await _siteCrudService.FindByIdAsync(site.Id);
            if (item == null)
            {
                return NotFound(new Response<Site>("Site does not exist"));
            }
            if (User.IsInRole("Manager"))
            {
                if (item.Name != site.Name) return BadRequest();
            }

            item.Name = site.Name;
            item.AreaId = site.AreaId;
            item.Address = site.Address;
            item.BuisnessHours = site.BuisnessHours;
            item.LastSeen = DateTime.Now;
            item.IsActive = site.IsActive;

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