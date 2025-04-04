using MongoDB.Bson;
using MongoDB.Driver;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Providers.MongoDB.Utils;

namespace JournaledTodoList.WebApp
{
    public class MongoEventStorageCollection : CollectionBase<BsonDocument>
    {
        private const string FieldId = "_id";
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

        public long getCount()
        {
            return Collection.CountDocuments(Filter.Empty);
        }

        public async Task<(int Index, TEvent Event)> ReadAsync<TEvent>(int i)
        {
            var existing =
                await Collection.Find(Filter.Eq(FieldId, i))
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

        public async Task WriteAsync<TEvent>(int i, TEvent @event)
        {
            var newData = _serializer.Serialize(@event);

            var newETag = Guid.NewGuid().ToString();

            try
            {
                await Collection.UpdateOneAsync(
                    Filter.And(
                        Filter.Eq(FieldId, i)),
                    Update
                        .Set(FieldEtag, newETag)
                        .Set(FieldDoc, newData),
                    Upsert);
            }
            catch (MongoException ex)
            {
                if (ex.IsDuplicateKey())
                {

                    var document = new BsonDocument
                    {
                        [FieldId] = i,
                        [FieldEtag] = i,
                        [FieldDoc] = newData
                    };

                    await Collection.ReplaceOneAsync(Filter.Eq(FieldId, i), document, UpsertReplace);
                }
                else
                {
                    throw;
                }
            }
        }



    }
}
