using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Domain.Common
{
    public class BaseEntity : IBaseEntity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    }

    public interface IBaseEntity
    {         
        string Id { get; set; }
    }
}
