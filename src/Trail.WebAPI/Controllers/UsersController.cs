using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;
using Trail.Domain.Entities;
using Trail.Domain.Settings;
using Trail.Infrastructure.Services;
using Trail.Domain.Enums;
using Trail.Domain.Common;
using Trail.Application.Common.Wrappers;
using Trail.Application.Common.Models.DTO;
using System.Security.Claims;
using Trail.Application.Common.Filters;

namespace Trail.WebAPI.Controllers
{
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserCrudService _userSearchService;
        private readonly ICrudService<User> _userCrudService;        
        public UsersController(
            IUserService userService, 
            UserCrudService userSearchService,
            ICrudService<User> userCrudService, 
            IOptions<AppSettings> settings)
        {
            _userService = userService;
            _userCrudService = userCrudService;
            _userSearchService = userSearchService;
        }

        
        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Authenticate(AuthenticateModel model)
        {
            var user = _userService.Authenticate(model.UserName, model.Password);

            if (user == null)
                return BadRequest(new { message = "Username or password is incorrect." });

            if(user.IsActive == false)
                return BadRequest(new { message = "Your account is locked. Please contact your admin." });

            return Ok(user);
        }

        [Authorize]
        [HttpPost("register")]
        public async Task<IActionResult> Create(User user)
        {
            var userId = User.FindFirstValue("UserName");
            var loggedInUser = await _userCrudService.FindOneAsync(p => p.UserName == userId);
            user.CompanyId = loggedInUser.CompanyId;
            var createdUser = _userService.Create(user);

            if (user == null)
                return BadRequest(new { message = "User registration failed." });

            return Ok(createdUser);
        }

        [Authorize]
        [HttpPost("logout")]        
        public IActionResult Logout()
        {
            return Ok();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUser([FromQuery] PaginationFilter filter, string searchQuery = null)
        {
            var userId = User.FindFirstValue("UserName");
            var user = await _userCrudService.FindOneAsync(p => p.UserName == userId);

            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);

            var records = new RecordCount<User>();

            if (String.IsNullOrWhiteSpace(searchQuery))
            {
                 records = _userCrudService.FilterBy(validFilter, p => p.CompanyId == user.CompanyId);
            }           
            else
            {
                records = _userSearchService.FilterBy(validFilter, p => p.CompanyId == user.CompanyId, searchQuery.ToLower());
            }
            return Ok(records);
        }

        [Authorize]
        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword(AuthenticateModel model)
        {
            var user = _userCrudService.FindOne(p => p.UserName == model.UserName );

            if (user == null)
                return BadRequest(new { message = "User does not exist." });

            var updatedUser = _userService.ChangePasswordWithoutOldPassword(user, model.Password);

            var response = await _userCrudService.ReplaceOneAsync(updatedUser);

            return Ok(new Response<User>(response, "Password updated successfully"));

        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update(User user)
        {
            var item = await _userCrudService.FindByIdAsync(user.Id);

            if (item == null)
            {
                return NotFound(new Response<User>("User does not exist"));
            }

            item.FirstName = user.FirstName;
            item.LastName = user.LastName;
            item.UserName = user.UserName;
            item.Email = user.Email;
            item.Role = user.Role;
            item.SiteId = user.SiteId;

            var response = await _userCrudService.ReplaceOneAsync(item);

            return Ok(new Response<User>(response, "User updated successfully"));
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> ToogleDelete(ToggleDeleteDTO toggleDelete)
        {
            var item = await _userCrudService.FindByIdAsync(toggleDelete.Id);

            if (item == null)
            {
                return NotFound(new Response<User>("User does not exist"));
            }

            item.IsActive = toggleDelete.IsActive;

            var response = await _userCrudService.ReplaceOneAsync(item);

            return Ok(new Response<User>(response, "Operation successfull"));

        }

    }
}
