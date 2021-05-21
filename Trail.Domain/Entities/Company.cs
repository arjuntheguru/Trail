using FluentValidation;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; }
        public string RegistrationNo { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; } = true;
        public Address Address { get; set; }
    }

    public class CompanyValidator : AbstractValidator<Company>
    {
        public CompanyValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.RegistrationNo).NotEmpty();
            RuleFor(p => p.Email).EmailAddress();
            RuleFor(p => p.Address).NotNull();
            RuleFor(p => p.IsActive).NotEmpty();
        }
    }
}