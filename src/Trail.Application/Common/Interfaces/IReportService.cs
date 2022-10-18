using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Application.Common.Interfaces
{
    public interface IReportService
    {
        List<BsonDocument> Daily(string from, string to);
        List<BsonDocument> Daily2(string from, string to);
        string checkPipeline(string from,string to);
    }
}
