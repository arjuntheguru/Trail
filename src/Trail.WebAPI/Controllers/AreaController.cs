using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
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
    [Authorize(Roles = "Admin,Manager")]
    public class AreaController : ApiControllerBase
    {

        private readonly ICrudService<Area> _areaCrudService;

        public AreaController(ICrudService<Area> areaCrudService)
        {
            _areaCrudService = areaCrudService;
        }

        [HttpGet("{companyId}")]
        public IActionResult GetArea(string companyId)
        {
            var areas = new List<Area>();

            areas = _areaCrudService.FilterBy(p => p.CompanyId == companyId).ToList();

            return Ok(areas);            
        }

        [HttpPost]
        public async Task<IActionResult> Create(Area area)
        {
            var id = await _areaCrudService.InsertOneAsync(area);

            var response = new Response<string>(id, "Area created successfully");

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {           

            if (id == null)
            {
                return BadRequest();
            }

            await _areaCrudService.DeleteOneAsync(p => p.Id == id);


            return Ok(new Response<string>(id, "Area deleted successfully"));

        }
    }
}
