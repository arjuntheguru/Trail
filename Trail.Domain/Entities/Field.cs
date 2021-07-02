using System;
using System.Collections.Generic;
using System.Text;
using Trail.Domain.Enums;

namespace Trail.Domain.Entities
{
    public class Field
    {
        public string Title { get; set; }
        public FieldType FieldType { get; set; }
        public string Value { get; set; }
        public IEnumerable<string> Options { get; set; }
    }
}
