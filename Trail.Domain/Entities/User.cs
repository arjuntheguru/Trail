using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Salt { get; set; }
        public string Role { get; set; } = Common.Role.Team;
        public string CompanyId { get; set; }
    }
}
