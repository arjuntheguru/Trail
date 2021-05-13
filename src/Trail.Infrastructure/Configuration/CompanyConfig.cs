using MongoDB.Driver;
using PluralizeService.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trail.Domain.Entities;
using Trail.Domain.Settings;

namespace Trail.Infrastructure.Configuration
{
    public static class CompanyConfig
    {
        public static void Config(IMongoDatabase database)
        {            
            var companyCollection = database.GetCollection<Company>("Companies");

            var indexModels = new List<CreateIndexModel<Company>>();

            var nameKey = Builders<Company>.IndexKeys.Ascending(p => p.Name);
            var registrationKey = Builders<Company>.IndexKeys.Ascending(p => p.Name);
            var indexOptions = new CreateIndexOptions { Unique = true,  };

            indexModels.Add(new CreateIndexModel<Company>(nameKey, indexOptions));
            
            companyCollection.Indexes.CreateMany(indexModels);
        }
        
    }
}
