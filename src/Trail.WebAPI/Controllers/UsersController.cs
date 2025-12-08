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
using System.Linq.Expressions;

namespace Trail.WebAPI.Controllers
{
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserCrudService _userSearchService;
        private readonly ICrudService<User> _userCrudService;
        private readonly ICrudService<Company> _companyCrudService;
        public UsersController(
            IUserService userService,
            UserCrudService userSearchService,
            ICrudService<User> userCrudService,
            ICrudService<Company> companyCrudService,
            IOptions<AppSettings> settings)
        {
            _userService = userService;
            _userCrudService = userCrudService;
            _userSearchService = userSearchService;
            _companyCrudService = companyCrudService;
        }


        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Authenticate(AuthenticateModel model)
        {
            var user = _userService.Authenticate(model.UserName, model.Password);

            if (user == null)
                return BadRequest(new { message = "Username or password is incorrect." });

            if (user.IsActive == false)
                return BadRequest(new { message = "Your account is locked. Please contact your admin." });

            if (!string.IsNullOrEmpty(user.CompanyId))
            {
                var company = _companyCrudService.FindById(user.CompanyId);
                if (company == null || !company.IsActive)
                {
                    return BadRequest(new { message = "Your company is inactive. Please contact your administrator." });
                }
            }

            return Ok(user);
        }

        [Authorize]
        [HttpPost("register")]
        public async Task<IActionResult> Create(User user)
        {
            var userId = User.FindFirstValue("UserName");
            var loggedInUser = await _userCrudService.FindOneAsync(p => p.UserName == userId);
            
            // Only SuperAdmin and Admin can create users
            if (loggedInUser == null || (loggedInUser.Role != Role.SuperAdmin && loggedInUser.Role != Role.Admin))
            {
                return Forbid();
            }
            
            if (loggedInUser.CompanyId != null)
            {
                user.CompanyId = loggedInUser.CompanyId;
            }
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
        public async Task<IActionResult> GetUser([FromQuery] PaginationFilter filter, string searchQuery = null, string companyId = null, string role = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserName");
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User identifier not found" });
                }

                var user = await _userCrudService.FindOneAsync(p => p.UserName == userId || p.Id == userId);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var userRole = user.Role?.ToLower() ?? "";
                Expression<Func<User, bool>> filterExpression;

                if (userRole.Contains("super"))
                {
                    // Super admin sees all users, but can filter by companyId and role if provided
                    if (!string.IsNullOrEmpty(companyId) && !string.IsNullOrEmpty(role))
                    {
                        filterExpression = p => p.CompanyId == companyId && p.Role != null && p.Role.ToLower() == role.ToLower();
                    }
                    else if (!string.IsNullOrEmpty(companyId))
                    {
                        filterExpression = p => p.CompanyId == companyId;
                    }
                    else if (!string.IsNullOrEmpty(role))
                    {
                        filterExpression = p => p.Role != null && p.Role.ToLower() == role.ToLower();
                    }
                    else
                    {
                        filterExpression = p => true;
                    }
                }
                else if (userRole == "admin")
                {
                    // Admin sees all users in same company (except SuperAdmin)
                    // If Admin has no CompanyId, they see all non-SuperAdmin users
                    // If companyId is provided, filter by it (but only if it matches their company or they have no company)
                    if (!string.IsNullOrEmpty(companyId))
                    {
                        // Only allow filtering by companyId if user has no company or it matches their company
                        if (string.IsNullOrEmpty(user.CompanyId) || user.CompanyId == companyId)
                        {
                            // Add role filter if provided
                            if (!string.IsNullOrEmpty(role))
                            {
                                filterExpression = p => p.CompanyId == companyId &&
                                                      p.Role != null &&
                                                      !p.Role.ToLower().Contains("super") &&
                                                      p.Role.ToLower() == role.ToLower() &&
                                                      p.Id != user.Id; // Exclude self
                            }
                            else
                            {
                                filterExpression = p => p.CompanyId == companyId &&
                                                      p.Role != null &&
                                                      !p.Role.ToLower().Contains("super") &&
                                                      p.Id != user.Id; // Exclude self
                            }
                        }
                        else
                        {
                            return Unauthorized(new { message = "You are not authorized to view users from this company" });
                        }
                    }
                    else if (string.IsNullOrEmpty(user.CompanyId))
                    {
                        // Add role filter if provided
                        if (!string.IsNullOrEmpty(role))
                        {
                            filterExpression = p => p.Role != null &&
                                                  !p.Role.ToLower().Contains("super") &&
                                                  p.Role.ToLower() == role.ToLower() &&
                                                  p.Id != user.Id; // Exclude self
                        }
                        else
                        {
                            filterExpression = p => p.Role != null &&
                                                  !p.Role.ToLower().Contains("super") &&
                                                  p.Id != user.Id; // Exclude self
                        }
                    }
                    else
                    {
                        // Add role filter if provided
                        if (!string.IsNullOrEmpty(role))
                        {
                            filterExpression = p => p.CompanyId == user.CompanyId &&
                                                  p.Role != null &&
                                                  !p.Role.ToLower().Contains("super") &&
                                                  p.Role.ToLower() == role.ToLower() &&
                                                  p.Id != user.Id; // Exclude self
                        }
                        else
                        {
                            filterExpression = p => p.CompanyId == user.CompanyId &&
                                                  p.Role != null &&
                                                  !p.Role.ToLower().Contains("super") &&
                                                  p.Id != user.Id; // Exclude self
                        }
                    }
                }
                else
                {
                    // Regular users see non-manager, non-admin users in their sites
                    var userSiteIds = user.SiteId ?? new System.Collections.Generic.List<string>();
                    filterExpression = p => p.CompanyId == user.CompanyId &&
                                          p.Role != null &&
                                          p.Role.ToLower() != "manager" &&
                                          p.Role.ToLower() != "admin" &&
                                          p.SiteId != null &&
                                          userSiteIds.Any(userSiteId => p.SiteId.Contains(userSiteId)) &&
                                          p.Id != user.Id; // Exclude self
                }

                var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);
                RecordCount<User> records;

                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    records = _userCrudService.FilterBy(validFilter, filterExpression);
                }
                else
                {
                    records = _userSearchService.FilterBy(validFilter, filterExpression, searchQuery.ToLower());
                }

                return Ok(new
                {
                    Success = true,
                    Records = records.Records,
                    Count = records.Count,
                    CurrentPage = validFilter.PageNumber,
                    PageSize = validFilter.PageSize,
                    TotalPages = (int)Math.Ceiling(records.Count / (double)validFilter.PageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching users", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword(AuthenticateModel model)
        {
            var user = _userCrudService.FindOne(p => p.UserName == model.UserName);

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

            var loggedInUserName = User.FindFirstValue("UserName");
            var loggedInUser = await _userCrudService.FindOneAsync(p => p.UserName == loggedInUserName);

            if (loggedInUser == null)
            {
                return Forbid();
            }

            var isSuperAdmin = loggedInUser.Role == Role.SuperAdmin;
            var isAdmin = loggedInUser.Role == Role.Admin;
            var isUpdatingSelf = loggedInUser.Id == user.Id;

            // Authorization: SuperAdmin and Admin can update any user, regular users can only update themselves
            if (!isSuperAdmin && !isAdmin && !isUpdatingSelf)
            {
                return Forbid();
            }

            item.FirstName = user.FirstName;
            item.LastName = user.LastName;
            item.UserName = user.UserName;
            item.Email = user.Email;
            item.Role = isSuperAdmin ? user.Role : item.Role;
            item.SiteId = isSuperAdmin ? user.SiteId : item.SiteId;

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
