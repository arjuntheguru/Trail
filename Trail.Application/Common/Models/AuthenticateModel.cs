using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trail.Application.Common.Models
{
    public class AuthenticateModel
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class AuthenticateModelValidator : AbstractValidator<AuthenticateModel>
    {
        public AuthenticateModelValidator()
        {
            RuleFor(p => p.UserName).NotEmpty();
            RuleFor(p => p.Password).NotEmpty();
        }
    }
}
