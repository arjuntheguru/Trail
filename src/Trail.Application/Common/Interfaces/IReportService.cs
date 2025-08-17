using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Trail.Application.Common.Interfaces
{
    public interface IReportService
    {
        Task<List<BsonDocument>> Daily(string from, string to);
        List<BsonDocument> Daily2(string from, string to);
        string checkPipeline(string from,string to);
    }
}
