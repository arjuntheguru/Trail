using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trail.Application.Common.Filters
{
    public class PaginationFilter
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public PaginationFilter()
        {
            this.PageNumber = 1;
            this.PageSize = 10;
        }

        public PaginationFilter(int pageNumber, int pageSize)
        {
            this.PageNumber = pageNumber < 1 ? 1 : pageNumber;

            if (PageSize < 0)
            {
                this.PageSize = 10;
            }

            this.PageSize = pageSize > 20 ? 20 : pageSize;
        }
    }
}
