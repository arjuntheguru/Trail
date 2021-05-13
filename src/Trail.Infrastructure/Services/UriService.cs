using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Text;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Models;

namespace Trail.Infrastructure.Services
{
    public class UriService : IUriService
    {
        private readonly string _baseUri;

        public UriService(string baseUri)
        {
            _baseUri = baseUri;
        }

        public Uri GetPageUri(PaginationFilter filter, string route, RequestParameter[] parameters)
        {
            var _enpointUri = new Uri(string.Concat(_baseUri, route));
            var modifiedUri = QueryHelpers.AddQueryString(_enpointUri.ToString(), "pageNumber", filter.PageNumber.ToString());
            modifiedUri = QueryHelpers.AddQueryString(modifiedUri, "pageSize", filter.PageSize.ToString());
            foreach (var parameter in parameters)
            {
                modifiedUri = QueryHelpers.AddQueryString(modifiedUri, parameter.ParameterName, parameter.ParameterValue.ToString());
            }
            return new Uri(modifiedUri);
        }
    }
}
