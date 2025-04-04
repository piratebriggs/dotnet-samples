using MongoDB.Bson;
using MongoDB.Driver;
using Orleans.Providers.MongoDB.Reminders.Store;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Providers.MongoDB.Utils;

namespace JournaledTodoList.WebApp
{
    public class MongoEventStorageCollection : CollectionBase<BsonDocument>
    {
        private const string FieldId = "_id";
        private const string FieldGrainId = "_grainId";
        private const string FieldDoc = "_doc";
        private const string FieldEtag = "_etag";
        private readonly string _collectionName;
        private readonly IGrainStateSerializer _serializer;
        public MongoEventStorageCollection(
            IMongoClient mongoClient,
            string databaseName,
            string collectionName,
            Action<MongoCollectionSettings> collectionConfigurator,
            bool createShardKey,
            IGrainStateSerializer serializer) : base(mongoClient, databaseName, collectionConfigurator, createShardKey)
        {
            _collectionName = collectionName;
            _serializer = serializer;
        }

        protected override string CollectionName()
        {
            return _collectionName;
        }

        protected override void SetupCollection(IMongoCollection<BsonDocument> collection) 
        {
            collection.Indexes.CreateOne(
            new CreateIndexModel<BsonDocument>(Index.Ascending(new StringFieldDefinition<BsonDocument>(FieldGrainId) ),
              new CreateIndexOptions
              {
                  Name = $"By{FieldGrainId}"
              }));
        }

        public long GetCount(GrainId grainId)
        {
            return Collection.CountDocuments(Filter.Eq(FieldGrainId, grainId.ToString()));
        }

        public async Task<(int Index, TEvent Event)> ReadAsync<TEvent>(GrainId grainId, int i)
        {
            var eventKey = $"{grainId.ToString()}/{i}";
            
            var existing =
                await Collection.Find(Filter.Eq(FieldId, eventKey))
                    .FirstOrDefaultAsync();

            if (existing == null)
            {
                return (-1, default)!;
            }

            TEvent result = default!;

            if (existing.Contains(FieldDoc))
            {
                //grainState.ETag = existing[FieldEtag].AsString;

                result = _serializer.Deserialize<TEvent>(existing[FieldDoc]);
            }
            else
            {
                existing.Remove(FieldId);

                result = _serializer.Deserialize<TEvent>(existing);
            }

            return (i, result);
        }

        public async Task WriteAsync<TEvent>(GrainId grainId, int i, TEvent @event)
        {
            var eventKey = $"{grainId.ToString()}/{i}";

            var newData = _serializer.Serialize(@event);

            var newETag = Guid.NewGuid().ToString();

            try
            {
                await Collection.UpdateOneAsync(
                    Filter.And(
                        Filter.Eq(FieldId, eventKey)),
                    Update
                        .Set(FieldGrainId, grainId.ToString())
                        .Set(FieldEtag, newETag)
                        .Set(FieldDoc, newData),
                    Upsert);
            }
            catch (MongoException)
            {
                throw;
            }
        }



    }
}
