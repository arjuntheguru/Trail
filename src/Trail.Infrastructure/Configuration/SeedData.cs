using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trail.Application.Common.Interfaces;
using Trail.Domain.Common;
using Trail.Domain.Entities;

namespace Trail.Infrastructure.Configuration
{
    public class SeedData
    {
        private readonly IUserService _userService;
        private readonly ICrudService<User> _userCrudService;
        public SeedData(IUserService userService, ICrudService<User> userCrudService)
        {
            _userService = userService;
            _userCrudService = userCrudService;
        }

        public void Run()
        {
            AddSuperAdmin();
        }

        private void AddSuperAdmin()
        {
            var user = _userCrudService.FilterBy(p => p.Role == Role.SuperAdmin).SingleOrDefault();

            if (user == null)
            {
                var superadmin = new User
                {
                    FirstName = "Super",
                    LastName = "Admin",
                    UserName = "arjuntheguru",
                    Email = "superadmin@fsl.com",
                    Password = "Arjun@123",
                    Role = Role.SuperAdmin
                };

                _userService.Create(superadmin);
            }

        }
    }
}
