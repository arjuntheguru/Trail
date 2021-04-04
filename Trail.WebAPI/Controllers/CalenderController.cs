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
    public class CalendarController : ApiControllerBase
    {
        private readonly ICrudService<Calendar> _calendarCrudService;
        private readonly IUriService _uriService;

        public CalendarController(
            ICrudService<Calendar> calendarCrudService,
            IUriService uriService)
        {
            _calendarCrudService = calendarCrudService;
            _uriService = uriService;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] PaginationFilter filter)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _calendarCrudService.AsQueryable(validFilter);

            var pagedReponse = PaginationHelper.CreatePagedReponse<Calendar>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, Array.Empty<RequestParameter>());

            return Ok(pagedReponse);
        }

        [HttpGet("{siteId}")]
        public IActionResult GetCalendarFromCompanyId([FromQuery] PaginationFilter filter, string siteId)
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = _calendarCrudService.FilterBy(validFilter, p => p.SiteId == siteId);

            var requestParameters = new ArrayList
            {
                new RequestParameter { ParameterName = "siteId", ParameterValue = siteId }
            };

            var pagedReponse = PaginationHelper.CreatePagedReponse<Calendar>(records.Records.ToList(), validFilter, records.Count, _uriService, this.Route, requestParameters.ToArray(typeof(RequestParameter)) as RequestParameter[]);

            return Ok(pagedReponse);
        }

        [HttpGet("{siteId}/{date}")]
        public async Task<IActionResult> GetCalendarFromDate(string siteId, DateTime date)
        {

            var record = await _calendarCrudService.FindOneAsync(p => p.SiteId == siteId && p.Date == date);

            if(record == null)
            {
                return NotFound(new Response<Calendar>("Not found"));
            }

            var response = new Response<Calendar>(record, $"Fetched calendar for {date} successfully");

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Calendar calendar)
        {

            var id = await _calendarCrudService.InsertOneAsync(calendar);

            var response = new Response<string>(id, "Calender created successfully");

            return Ok(response);
        }
        [HttpPut]
        public async Task<IActionResult> Update(Calendar calendar)
        {
            var item = await _calendarCrudService.FindByIdAsync(calendar.Id);

            if (item == null)
            {
                return NotFound(new Response<Calendar>("Calendar does not exist"));
            }

            item.TaskList = calendar.TaskList;

            var response = await _calendarCrudService.ReplaceOneAsync(item);

            return Ok(new Response<string>(response, "Calendar updated successfully"));
        }
    }
}
