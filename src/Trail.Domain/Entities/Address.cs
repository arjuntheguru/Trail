using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Trail.Domain.Entities
{
    public class Address
    {
        public string StreetAddress { get; set; }

        public string AddressLine2 { get; set; }
        
        public string City { get; set; }

        public string State { get; set; }

        public string Country { get; set; }

        public string ZIP { get; set; }
    }

    public class AddressValidator : AbstractValidator<Address>
    {
        public AddressValidator()
        {
            RuleFor(p => p.StreetAddress).NotEmpty();
            RuleFor(p => p.City).NotEmpty();
        }
    }
}
