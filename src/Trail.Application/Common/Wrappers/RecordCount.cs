using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Trail.Application.Common.Wrappers
{
    public class RecordCount<T>
    {
        public int Count { get; set; }
        public IEnumerable<T> Records { get; set; }
    }
}
