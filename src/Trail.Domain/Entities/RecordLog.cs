using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Common;

namespace Trail.Domain.Entities
{
    public class RecordLog : BaseEntity
    {
        public string Title { get; set; }
        public IEnumerable<Field> Fields { get; set; }
    }
}
