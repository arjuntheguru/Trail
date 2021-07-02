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
    [Authorize(Roles = "Admin")]
    public class TagController : ApiControllerBase
    {

        private readonly ICrudService<Tag> _tagCrudService;

        public TagController(ICrudService<Tag> tagCrudService)
        {
            _tagCrudService = tagCrudService;
        }

        [HttpGet("{companyId}")]
        public IActionResult GetTag(string companyId)
        {
            var tags = new List<Tag>();

            tags = _tagCrudService.FilterBy(p => p.CompanyId == companyId).ToList();

            return Ok(tags);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Tag tag)
        {
            var id = await _tagCrudService.InsertOneAsync(tag);

            var response = new Response<string>(id, "Tag created successfully");

            return Ok(response);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(Tag tag)
        {
            var item = await _tagCrudService.FindByIdAsync(tag.Id);

            if (item == null)
            {
                return NotFound(new Response<Tag>("Tag does not exist"));
            }

            item.Name = tag.Name;           

            var response = await _tagCrudService.ReplaceOneAsync(item);

            return Ok(new Response<Tag>(response, "Tag updated successfully"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {

            if (id == null)
            {
                return BadRequest();
            }

            await _tagCrudService.DeleteOneAsync(p => p.Id == id);


            return Ok(new Response<string>(id, "Tag deleted successfully"));

        }
    }
}
