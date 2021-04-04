    using MongoDB.Bson;
using MongoDB.Driver;
using PluralizeService.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Interfaces;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Common;
using Trail.Domain.Entities;
using Trail.Domain.Settings;
using Trail.Infrastructure.Configuration;

namespace Trail.Infrastructure.Services
{
    public class CrudService<TDocument> : ICrudService<TDocument> where TDocument : IBaseEntity
    {
        private readonly IMongoCollection<TDocument> _collection;

        public CrudService(IDatabaseSettings settings)
        {
            var database = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
            _collection = database.GetCollection<TDocument>(GetCollectionName(typeof(TDocument)));

            CompanyConfig.Config(database);

        }

        private protected string GetCollectionName(Type documentType)
        {
            return PluralizationProvider.Pluralize(documentType.Name);
        }

        public virtual RecordCount<TDocument> AsQueryable(PaginationFilter filter)
        {
            var result = new RecordCount<TDocument>
            {
                Count = _collection.AsQueryable().Count(),
                Records = _collection.AsQueryable()
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
            };

            return result;
        }

        public virtual RecordCount<TDocument> FilterBy(PaginationFilter filter,
            Expression<Func<TDocument, bool>> filterExpression)
        {
            var result = new RecordCount<TDocument>
            {
                Count = (int)_collection.Find(filterExpression).CountDocuments(),
                Records = _collection.Find(filterExpression).ToEnumerable()
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)               
               
            };

            return result;
        }

        public virtual IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression)
        {
            return _collection.Find(filterExpression).Project(projectionExpression).ToEnumerable();
        }

        public virtual TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.Find(filterExpression).FirstOrDefault();
        }

        public virtual Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return Task.Run(() => _collection.Find(filterExpression).FirstOrDefaultAsync());
        }

        public virtual TDocument FindById(string id)
        {          
            return _collection.Find<TDocument>(p => p.Id.Equals(id)).SingleOrDefault();
        }

        public virtual async Task<TDocument> FindByIdAsync(string id)
        {
            return await _collection.Find<TDocument>(p => p.Id.Equals(id)).SingleOrDefaultAsync();
        }


        public virtual string InsertOne(TDocument document)
        {
            _collection.InsertOne(document);

            return document.Id;
        }

        public virtual async Task<string> InsertOneAsync(TDocument document)
        {
            await _collection.InsertOneAsync(document);
            return document.Id;
        }

        public void InsertMany(ICollection<TDocument> documents)
        {
            _collection.InsertMany(documents);
        }


        public virtual async Task InsertManyAsync(ICollection<TDocument> documents)
        {
            await _collection.InsertManyAsync(documents);
        }

        public string ReplaceOne(TDocument document)
        {
            var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, document.Id);
            var response = _collection.FindOneAndReplace(filter, document);
            return response.Id;
        }

        public virtual async Task<string> ReplaceOneAsync(TDocument document)
        {
            var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, document.Id);
            var response = await _collection.FindOneAndReplaceAsync(filter, document);
            return response.Id;
        }

        public void DeleteOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            _collection.FindOneAndDelete(filterExpression);
        }

        public Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return Task.Run(() => _collection.FindOneAndDeleteAsync(filterExpression));
        }

        public void DeleteById(string id)
        {            
            var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
            _collection.FindOneAndDelete(filter);
        }

        public Task DeleteByIdAsync(string id)
        {
            return Task.Run(() =>
            {              
                var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
                _collection.FindOneAndDeleteAsync(filter);
            });
        }

        public void DeleteMany(Expression<Func<TDocument, bool>> filterExpression)
        {
            _collection.DeleteMany(filterExpression);
        }

        public Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return Task.Run(() => _collection.DeleteManyAsync(filterExpression));
        }
    }
}
