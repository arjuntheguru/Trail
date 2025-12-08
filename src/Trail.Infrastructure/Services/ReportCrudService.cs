using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using PluralizeService.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trail.Application.Common.Interfaces;
using Trail.Domain.Entities;
using Trail.Domain.Settings;

namespace Trail.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly IMongoCollection<Calendar> _collection;
        private readonly IMongoDatabase _database;

        public ReportService(IDatabaseSettings settings)
        {

            _database = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
            _collection = _database.GetCollection<Calendar>(GetCollectionName(typeof(Calendar)));
        }
        public class Score
        {
            public ObjectId _id { get; set; }
            public int score { get; set; }
            public DateTime date { get; set; }

        }
        public class DailyReport
        {
            public string _id { get; set; }
            public float avgScore { get; set; }
            public IEnumerable<Score> score { get; set; }
        }
        public string GetCollectionName(Type documentType)
        {
            return PluralizationProvider.Pluralize(documentType.Name);
        }

        public async Task<List<BsonDocument>> Daily(string from, string to)
        {
            var fromDate = DateTime.Parse(from);
            var toDate = DateTime.Parse(to);
            
            // Fetch all matching calendar entries first
            var calendars = await _collection
                .Find(c => c.Date >= fromDate && c.Date <= toDate)
                .ToListAsync();
            
            // Group by SiteId in memory
            var grouped = calendars
                .GroupBy(c => c.SiteId)
                .Select(g => new BsonDocument
                {
                    { "_id", g.Key },
                    { "avgScore", g.Average(c => c.ScoreValue) },
                    { "scores", new BsonArray(g.Select(c => new BsonDocument
                    {
                        { "_id", c.Id },
                        { "score", c.ScoreValue },
                        { "date", c.Date }
                    }))}
                })
                .ToList();
            
            return grouped;
        }

        public List<BsonDocument> Daily2(string from, string to)
        {
            var pipeline = new BsonDocument[]{
                new BsonDocument("$group", new BsonDocument{
                    { "_id", "$SiteId" }
                })
            };

            return _collection.Aggregate<BsonDocument>(pipeline)
                .ToList();
        }
        public string checkPipeline(string from, string to)
        {
            var Pipeline = new BsonDocument[] {
                new BsonDocument("$match",
                    new BsonDocument{
                        {
                            "Date",
                                new BsonDocument{
                                    {"$gte", DateTime.Parse(from) },
                                    {"$lte",DateTime.Parse(to)}
                                }
                        }
                    }),
                    new BsonDocument("$group", new BsonDocument{
                        { "_id","$SiteId"},
                        { "avgScore", new BsonDocument("$avg","$ScoreValue") },
                        {"score",
                            new BsonDocument("$push",
                                new BsonDocument{
                                    { "_id", "$_id" },
                                    {"score","$ScoreValue"},
                                    {"date","$Date"}
                                }
                            )
                        }
                    })
            };
            return Pipeline.ToJson();
        }
    }
}

