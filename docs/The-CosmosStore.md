# The CosmosStore

### What is it and why do I care?

The main data context you will be working with while using Cosmonaut is the CosmosStore. The CosmosStore requires you to provide the entity model that it will be working with. 

For example if I only wanted to work with the class `Book` my CosmosStore initialisation would look like this:

```c#
ICosmosStore<Book> bookStore = new CosmosStore<Book>(cosmosSettings)
```

> But what is the context of the CosmosStore? What will I get if I query for all the items in a CosmosStore?

The CosmosStore's boundaries can be one of two. 

* One entity is stored in it's own collection (ie books)
* One entity is stored in a shared collection that other entities live as well (ie library)

The choice to go with one or the other is completely up to you and it comes down to partitioning strategy, cost and flexibility when it comes to scaleability.

### CosmosStore's lifetime

CosmosStores should be registered as *singletons* in your system. This will achieve optimal performance. If you are using a dependency injection framework make sure they are registered as singletons and if you don't, just make sure you don't dispose them and you keep reusing them.

### CosmosStoreSettings

The CosmosStore can be initialised in multiple ways but the recommended one is by providing the `CosmosStoreSettings` object.

The `CosmosStoreSettings` object can be initialised requires 3 parameters in order to be created. The database name, the Cosmos DB endpoint Uri and the auth key.

```c#
 var cosmosSettings = new CosmosStoreSettings("<<databaseName>>", "<<cosmosUri>>", "<<authkey>>");
```

There are other optional settings you can provide such as:

* ConnectionPolicy - The connection policy for this CosmosStore
* ConsistencyLevel - The level of consistency for this CosmosStore
* IndexingPolicy - The indexing policy for this CosmosStore if it's collection in not yet created
* DefaultCollectionThroughput - The default throughput for this CosmosStore if it's collection in not yet created
* JsonSerializerSettings - The object to json serialization settings
* InfiniteRetries - Whether you want infinite retries on throttled requests
* CollectionPrefix - A prefix prepended on the collection name

### CosmosResponse and response handling

By default, Cosmos DB throws exceptions for any bad operation. This includes reading documents that don't exist, pre condition failures or trying to add a document that already exists.

This makes response handing really painful so Cosmonaut changes that.

Instead of throwing an excpetion Cosmonaut wraps the responses into it's own response called `CosmosResponse`.

This object contains the following properties:

* IsSuccess - Indicates whether the operation was successful or not
* CosmosOperationStatus - A Cosmonaut enum which indicates what the status of the response is
* ResourceResponse - The ResourceResponse<Document> that contains things like RU charge, Etag, headers and all the other info that the response would normally have
* Entity - The object you used for this operation
* Exception - The exception that caused this response to fail

It also has an implicit operation which, if present, will return the entity itself.

#### CosmosOperationStatus

The CosmosOperationStatus operation status can have one of 5 values.

* Success - The operation was successful
* RequestRateIsLarge - Your CosmosDB is under heavy load and it can't handle the request
* ResourceNotFound - The item you tried to update/upsert was not found
* PreconditionFailed - The Etag that you provided is different from the document one in the database indicating that it has been changed
* Conflict - You are trying to add an item that already exists

### Notes

The CosmosStore also exposes the underlying CosmonautClient that it's using to perform operations so you can use that for any other operations you want to make against Cosmos DB. You need to know though that the CosmosStore's context is only limited for it's own methods. Once you use the CosmonautClient or the DocumentClient you are outside of the limitations of CosmosStore so be careful.