using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trail.Application.Common.Interfaces;
using Trail.Domain.Entities;
using Trail.Infrastructure.Services;

namespace Trail.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecordsController : ApiControllerBase
    {
        private readonly ICrudService<Site> _siteCrudService;
        private readonly ICrudService<User> _userCrudService;
        private readonly SiteCrudService _siteSearchService;
        private readonly IUriService _uriService;
        private readonly IReportService _reportService;
        private readonly ICrudService<Calendar> _calendarCrudService;


        public RecordsController(
            ICrudService<Site> siteCrudService,
            ICrudService<User> userCrudService,
            IUriService uriService,
            SiteCrudService siteSearchService,
            IReportService reportService,
            ICrudService<Calendar> calendarCrudService)
        {
            _siteCrudService = siteCrudService;
            _uriService = uriService;
            _userCrudService = userCrudService;
            _siteSearchService = siteSearchService;
            _reportService = reportService;
            _calendarCrudService = calendarCrudService;
        }

        //[Authorize(Roles = "Admin")]
        [HttpGet("sites")]
        public IActionResult GetAll()
        {
            //var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _siteCrudService.AsQueryable();

            //var pagedReponse = PaginationHelper.CreatePagedReponse<Site>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(records);
        }
        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar(string startdate, string enddate, int gap)
        {
            var records = await _reportService.Daily(startdate, enddate);
            return Ok(records);
        }
        [HttpGet("testBson")]
        public IActionResult GetBson()
        {
            var records = _reportService.checkPipeline("2020-01-01","2022-09-15");
            return Ok(records);

        }
        [HttpGet("testBson2")]
        public IActionResult GetBson2()
        {
            var records = _reportService.Daily2("abc", "def");
            return Ok(records);

        }
    }
}
