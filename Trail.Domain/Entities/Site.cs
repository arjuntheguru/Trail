﻿using FluentValidation;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class Site : BaseEntity
    {        
        public string Name { get; set; }
        public string CompanyId { get; set; }
        public bool IsActive { get; set; } = true;
        public int ScoreCount { get; set; } = 0;
        public int ScoreValue { get; set; } = 0;
        public Address Address { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public IEnumerable<BuisnessHour> BuisnessHours { get; set; }
    }

    public class SiteValidator : AbstractValidator<Site>
    {
        public SiteValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.CompanyId).NotEmpty();            
            RuleForEach(p => p.BuisnessHours).SetValidator(new BuisnessHourValidator());
        }
    }
}
