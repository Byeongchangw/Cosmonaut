﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace Cosmonaut
{
    public class CosmoStore<TEntity> : ICosmoStore<TEntity>
    {
        private readonly IDocumentClient _documentClient;
        private readonly string _databaseName;

        private readonly AsyncLazy<Database> _database;
        private readonly AsyncLazy<DocumentCollection> _collection;

        private readonly string _collectionName;

        public CosmoStore(IDocumentClient documentClient, string databaseName)
        {
            _documentClient = documentClient;
            _databaseName = databaseName;

            _collectionName = GetCollectionNameForEntity();

            _database = new AsyncLazy<Database>(async () => await GetOrCreateDatabaseAsync());
            _collection = new AsyncLazy<DocumentCollection>(async () => await GetOrCreateCollectionAsync());
        }
        
        public async Task<CosmosResponse> AddAsync(TEntity entity, RequestOptions requestOptions = null)
        {
            var collection = await _collection;

            var safeDocument = GetCosmosDbFriendlyEntity(entity);

            ResourceResponse<Document> addedDocument = await _documentClient.CreateDocumentAsync(collection.SelfLink, safeDocument, requestOptions);
            return new CosmosResponse(addedDocument);
        }

        public async Task<IEnumerable<CosmosResponse>> AddRangeAsync(params TEntity[] entities)
        {
            return await AddRangeAsync((IEnumerable<TEntity>)entities);
        }

        public async Task<IEnumerable<CosmosResponse>> AddRangeAsync(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var responses = new List<CosmosResponse>();

            foreach (var entity in entities)
            {
                responses.Add(await AddAsync(entity));
            }

            return responses;
        }

        public async Task<IQueryable<TEntity>> AsQueryableAsync()
        {
            return _documentClient.CreateDocumentQuery<TEntity>((await _collection).DocumentsLink)
                .AsQueryable();
        }

        public async Task<List<TEntity>> ToListAsync(Func<TEntity, bool> predicate = null)
        {
            if (predicate == null)
                predicate = entity => true;

            return _documentClient.CreateDocumentQuery<TEntity>((await _collection).DocumentsLink)
                .Where(predicate)
                .ToList();
        }

        public async Task<TEntity> FirstOrDefaultAsync(Func<TEntity, bool> predicate)
        {
            return
                _documentClient.CreateDocumentQuery<TEntity>((await _collection).DocumentsLink)
                    .Where(predicate)
                    .AsEnumerable()
                    .FirstOrDefault();
        }
        
        public IDocumentClient DocumentClient => _documentClient;

        internal async Task<Database> GetOrCreateDatabaseAsync()
        {
            Database database = _documentClient.CreateDatabaseQuery()
                .Where(db => db.Id == _databaseName).ToArray().FirstOrDefault();
            if (database == null)
            {
                database = await _documentClient.CreateDatabaseAsync(
                    new Database { Id = _databaseName });
            }

            return database;
        }

        internal async Task<DocumentCollection> GetOrCreateCollectionAsync()
        {
            DocumentCollection collection = _documentClient.CreateDocumentCollectionQuery((await _database).SelfLink).Where(c => c.Id == _collectionName).ToArray().FirstOrDefault();

            if (collection == null)
            {
                collection = new DocumentCollection { Id = _collectionName };

                collection = await _documentClient.CreateDocumentCollectionAsync((await _database).SelfLink, collection);
            }

            return collection;
        }

        internal dynamic GetCosmosDbFriendlyEntity(TEntity entity)
        {
            var propertyInfos = entity.GetType().GetProperties();

            var containsJsonAttributeIdCount =
                propertyInfos.Count(x => x.GetCustomAttributes().ToList().Contains(new JsonPropertyAttribute("id")));

            if (containsJsonAttributeIdCount > 1)
                throw new ArgumentException("An entity can only have one cosmos db id. Only one [JsonAttribute(\"id\")] allowed per entity.");

            var idProperty = propertyInfos.FirstOrDefault(x =>
                x.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && x.PropertyType == typeof(string));

            if (idProperty != null && containsJsonAttributeIdCount == 1)
            {
                if (!idProperty.GetCustomAttributes<JsonPropertyAttribute>().Any(x =>
                    x.PropertyName.Equals("id", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(
                        "An entity can only have one cosmos db id. Either rename the Id property or remove the [JsonAttribute(\"id\")].");
                return entity;
            }

            if (idProperty == null || containsJsonAttributeIdCount != 0)
                return entity;

            if(idProperty.GetValue(entity) == null)
                idProperty.SetValue(entity, Guid.NewGuid().ToString());

            //TODO Clean this up. It is a very bad hack
            dynamic mapped = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(entity));

            SetTheCosmosDbIdBasedOnTheObjectIndex(entity, mapped, idProperty);
            
            RemovePotentialDuplicateIdProperties(mapped);

            return mapped;
        }

        internal static void SetTheCosmosDbIdBasedOnTheObjectIndex(TEntity entity, dynamic mapped, PropertyInfo idProperty)
        {
            if (mapped.id == null)
            {
                mapped.id = idProperty.GetValue(entity).ToString();
            }
        }

        internal static string GetCollectionNameForEntity()
        {
            var collectionNameAttribute = typeof(TEntity).GetCustomAttribute<CosmosCollectionAttribute>();
            
            var collectionName = collectionNameAttribute?.Name;

            return !string.IsNullOrEmpty(collectionName) ? collectionName : typeof(TEntity).Name.ToLower().Pluralize();
        }

        internal static void RemovePotentialDuplicateIdProperties(dynamic mapped)
        {
            if (mapped.Id != null)
                mapped.Remove("Id");

            if (mapped.ID != null)
                mapped.Remove("ID");

            if (mapped.iD != null)
                mapped.Remove("iD");
        }
    }
}