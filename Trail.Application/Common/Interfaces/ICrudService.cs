using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Trail.Application.Common.Filters;
using Trail.Application.Common.Wrappers;
using Trail.Domain.Common;

namespace Trail.Application.Common.Interfaces
{
    public interface ICrudService<TDocument> where TDocument : IBaseEntity
    {
        IEnumerable<TDocument> AsQueryable();
        RecordCount<TDocument> AsQueryable(PaginationFilter filter);

        IEnumerable<TDocument> FilterBy(Expression<Func<TDocument, bool>> filterExpression);

        RecordCount<TDocument> FilterBy(PaginationFilter filter,
            Expression<Func<TDocument, bool>> filterExpression);
        
        IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression);

        TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression);

        Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression);

        TDocument FindById(string id);

        Task<TDocument> FindByIdAsync(string id);

        string InsertOne(TDocument document);

        Task<string> InsertOneAsync(TDocument document);

        void InsertMany(ICollection<TDocument> documents);

        Task InsertManyAsync(ICollection<TDocument> documents);

        string ReplaceOne(TDocument document);

        Task<TDocument> ReplaceOneAsync(TDocument document);

        void DeleteOne(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression);

        //void DeleteById(string id);

        //Task DeleteByIdAsync(string id);

        void DeleteMany(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression);
    }
}
