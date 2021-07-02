﻿using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; }
        public string CompanyId { get; set; }
    }
}
