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

namespace Trail.WebAPI.Controllers
{
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly ICrudService<User> _userCrudService;
        private readonly AppSettings _settings;
        public UsersController(
            IUserService userService, 
            ICrudService<User> userCrudService, 
            IOptions<AppSettings> settings)
        {
            _userService = userService;
            _userCrudService = userCrudService;
            _settings = settings.Value;
        }

        
        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Authenticate(AuthenticateModel model)
        {
            var user = _userService.Authenticate(model.UserName, model.Password);

            if (user == null)
                return BadRequest(new { message = "Username or password is incorrect" });

            return Ok(user);
        }

        [Authorize]
        [HttpPost("register")]
        public IActionResult Create(User user)
        {
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
        [HttpGet("admin/{companyId}")]
        public IActionResult GetAdmin(string companyId)
        {
            var admins = _userCrudService.FilterBy(p => p.CompanyId == companyId && p.Role == Role.Admin);

            return Ok(admins);
        }

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

            var response = await _userCrudService.ReplaceOneAsync(item);

            return Ok(new Response<User>(response, "User updated successfully"));
        }
        
    }
}
