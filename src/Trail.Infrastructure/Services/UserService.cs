using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Helpers;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models.DTO;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Entities;
using Trail.Domain.Settings;

namespace Trail.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly AppSettings _appSettings;
        private readonly ICrudService<User> _userCrudService;

        public UserService(
            IOptions<AppSettings> appSettings,
            ICrudService<User> userCrudService)
        {
            _appSettings = appSettings.Value;
            _userCrudService = userCrudService;
        }

        public UserDTO Authenticate(string username, string password)
        {
            try
            {
                var user = _userCrudService.FindOne(u => u.UserName == username);

                if (user == null)
                {
                    return null;
                }

                if (ValidatePassword(user, password))
                {
                    return this.GenerateUserToken(user.WithoutPassword());
                }

                return null;
            }
            catch (Exception exception)
            {
                throw exception;
            }

        }

        public IEnumerable<User> GetAll(PaginationFilter filter)
        {
            var users = _userCrudService.AsQueryable(filter);
            return users.Records.WithoutPasswords();
        }

        public async Task<User> GetByUserNameAsync(string userName)
        {
            var user = await _userCrudService.FindOneAsync(p => p.UserName == userName);
            return user.WithoutPassword();
        }

        public UserDTO Create(User user)
        {
            var userId = _userCrudService.InsertOne(UserWithEncryptedPassword(user, user.Password));
            user.Id = userId;
            return GenerateUserToken(user);
        }

        private User UserWithEncryptedPassword(User user, string password)
        {
            var saltBytes = GenerateSalt(12);
            var passwordBytes = GenerateHash(Encoding.UTF8.GetBytes(password), saltBytes, 5, 5);
            user.Salt = Convert.ToBase64String(saltBytes);
            user.Password = Encoding.UTF8.GetString(passwordBytes, 0, passwordBytes.Length);
            return user;
        }

        private byte[] GenerateSalt(int length)
        {
            var bytes = new byte[length];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetNonZeroBytes(bytes);
            }

            return bytes;
        }

        private byte[] GenerateHash(byte[] password, byte[] salt, int iterations, int length)
        {
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                return deriveBytes.GetBytes(length);
            }
        }

        private UserDTO GenerateUserToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim("UserName", user.UserName),
                    new Claim("FirstName", user.FirstName),
                    new Claim("LastName", user.LastName),

                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);

            UserDTO userDTO = new UserDTO
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                IsActive = user.IsActive,
                Role = user.Role,
                Email = user.Email,
                CompanyId = user.CompanyId,
                SiteId = user.SiteId,
                Token = tokenHandler.WriteToken(token)
            };

            return userDTO; ;
        }

        private bool ValidatePassword(User user, string inputPassword)
        {
            var inputPasswordBytes = GenerateHash(Encoding.UTF8.GetBytes(inputPassword), Convert.FromBase64String(user.Salt), 5, 5);
            return user.Password == Encoding.UTF8.GetString(inputPasswordBytes);
        }

        public User ChangePasswordWithoutOldPassword(User user, string password)
        {
            return UserWithEncryptedPassword(user, password);
        }


    }
}
