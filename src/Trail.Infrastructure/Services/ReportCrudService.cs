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
            //return Pipeline.ToJson();
            //_collection.Aggregate([
            //    {
            //    $match: { "date":{ $gte: new ISODate} }
            //}
            //])
            //var data = await _collection.Aggregate<BsonDocument>(Pipeline).ToListAsync();
            var data = await _collection
             .Aggregate()
             .Match(c => c.Date >= DateTime.Parse(from) && c.Date <= DateTime.Parse(to))
             .Group(
                 key => key.SiteId,
                 g => new BsonDocument
                 {
                    { "_id", g.Key },
                    { "avgScore", g.Average(i => i.ScoreValue) },
                    { "scores", new BsonArray(
                        g.Select(x => new BsonDocument
                        {
                            { "_id", x.Id },
                            { "score", x.ScoreValue },
                            { "date", x.Date }
                        })
                    )}
                 }
             )
             .ToListAsync();

            return data;
        }

        public List<BsonDocument> Daily2(string from, string to)
        {
            var pipeline = new BsonDocument[]{
                new BsonDocument{{"$group", new BsonDocument{
                {
                    "_id","$SiteId"
                } } } } };


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

