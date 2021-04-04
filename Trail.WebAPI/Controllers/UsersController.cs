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

        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Create(User user)
        {
            var createdUser = _userService.Create(user);

            if (user == null)
                return BadRequest(new { message = "User registration failed." });

            return Ok(createdUser);
        }
    }
}
