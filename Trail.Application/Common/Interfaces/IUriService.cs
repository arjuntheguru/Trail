using System;
using System.Collections.Generic;
using System.Text;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Models;

namespace Trail.Application.Common.Interfaces
{
    public interface IUriService
    {
        public Uri GetPageUri(PaginationFilter filter, string route, RequestParameter[] parameters);
    }
}
