using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Models.DTO;
using Trail.Domain.Entities;

namespace Trail.Application.Common.Interfaces
{
    public interface IUserService
    {
        UserDTO Authenticate(string username, string password);
        UserDTO Create(User user);
        IEnumerable<User> GetAll(PaginationFilter filter);
        Task<User> GetByUserNameAsync(string userName);
        User ChangePasswordWithoutOldPassword(User user, string password);
    }
}
