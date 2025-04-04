using System.Collections.Concurrent;
using MongoDB.Driver;
using Orleans.EventSourcing.Snapshot;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Providers.MongoDB.Utils;

namespace JournaledTodoList.WebApp;

/// <summary>
/// Based on Orleans.Providers.MongoDB.StorageProviders.MongoGrainStorage
/// Uses existing IGrainStateSerializer to hydrate events back from storage.
/// </summary>
public class MongoStorage : IGrainEventStorage
{
    private readonly string _collectionPrefix = "Events";
    private readonly ConcurrentDictionary<string, MongoEventStorageCollection> collections = new ConcurrentDictionary<string, MongoEventStorageCollection>();
    private readonly MongoDBOptions options;
    private readonly IMongoClient mongoClient;
    private readonly IGrainStateSerializer serializer;

    public MongoStorage(
        IMongoClientFactory mongoClientFactory,
        MongoDBOptions options,
        IGrainStateSerializer serializer)
    {
        this.mongoClient = mongoClientFactory.Create(options, "Storage");
        this.options = options;
        this.serializer = serializer;
    }

    public async Task SaveEvents<TEvent>(string grainTypeName, GrainId grainReference, IEnumerable<TEvent> events, int expectedVersion)
    {
        var collection = GetCollection(grainTypeName);

        var startNewEvent = await EventsCount(grainTypeName, grainReference);

        var tasks = events
            .Select((e, i) => collection.WriteAsync(grainReference,startNewEvent + i, e))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    public async Task<List<TEvent>> ReadEvents<TEvent>(string grainTypeName, GrainId grainReference, int start, int count)
    {
        var collection = GetCollection(grainTypeName);

        var tasks = Enumerable.Range(start, count)
            .Select(i => collection.ReadAsync<TEvent>(grainReference, i))
            .ToArray();
        await Task.WhenAll(tasks);

        return tasks
            .Select(x => x.Result)
            .Where(x => x.Index > -1)
            .OrderBy(x => x.Index)
            .Select(x => x.Event)
            .ToList();
    }

    public Task<int> EventsCount(string grainTypeName, GrainId grainReference)
    {
        var container = GetCollection(grainTypeName);

        return Task.FromResult((int)container.GetCount(grainReference));
    }

    private MongoEventStorageCollection GetCollection(string grainTypeName)
    {
        var collectionName = $"{_collectionPrefix}{grainTypeName}";

        return collections.GetOrAdd(collectionName, x =>
            new MongoEventStorageCollection(
                mongoClient,
                options.DatabaseName,
                collectionName,
                options.CollectionConfigurator,
                options.CreateShardKeyForCosmos,
                serializer));
    }

}
