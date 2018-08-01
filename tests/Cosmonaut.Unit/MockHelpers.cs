﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security;
using System.Threading;
using Cosmonaut.Extensions;
using Cosmonaut.Testing;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosmonaut.Unit
{
    public static class MockHelpers
    {
        public static Document ItIsSameDocument(this Document document)
        {
            return It.Is<Document>(doc => doc.ToString() == document.ToString());
        }

        public static Mock<IDocumentClient> GetMockDocumentClient(string databaseName = "databaseName", string collectionName = "dummies")
        {
            var mockDocumentClient = new Mock<IDocumentClient>();
            var database = new Database {Id = databaseName};
            var collection = new DocumentCollection
            {
                Id = collectionName
            };
            collection.SetPropertyValue("resource", "docs");
            collection.SetPropertyValue("_self", "docs");
            var mockOffer = new Mock<Offer>();
            mockOffer.Object.SetPropertyValue("resource", "docs");
            mockOffer.Object.SetResourceTimestamp(DateTime.UtcNow);
            mockDocumentClient.Setup(x => x.AuthKey).Returns(new SecureString());
            mockDocumentClient.Setup(x => x.ServiceEndpoint).Returns(new Uri("http://test.com"));
            mockDocumentClient.Setup(x => x.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName), null))
                .ReturnsAsync(database.ToResourceResponse(HttpStatusCode.OK));
            mockDocumentClient.Setup(x => x.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), null))
                .ReturnsAsync(collection.ToResourceResponse(HttpStatusCode.OK));
            mockDocumentClient.Setup(x => x.CreateDatabaseQuery(null))
                .Returns(new EnumerableQuery<Database>(new List<Database> { database }));
            mockDocumentClient.Setup(x => x.CreateDocumentCollectionQuery(It.IsAny<string>(), null))
                .Returns(new EnumerableQuery<DocumentCollection>(new List<DocumentCollection> { collection }));
            var offerV2 = new OfferV2(mockOffer.Object, 400);
            offerV2.SetResourceTimestamp(DateTime.UtcNow);
            mockDocumentClient.Setup(x => x.CreateOfferQuery(null)).Returns(
                new EnumerableQuery<OfferV2>(new List<OfferV2>
                {
                    offerV2
                }));
            return mockDocumentClient;
        }

        public static void SetResourceTimestamp<T>(this T resource, DateTime dateTime) where T : Resource
        {
            resource?.SetPropertyValue("_ts", (object)(ulong)(DateTime.UtcNow - UnixStartTime).TotalSeconds);
        }

        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static CosmosStore<Dummy> ResponseSetup(IQueryable<Dummy> expected, IQueryable<Dummy> dataSource, ref Mock<IDocumentClient> mockDocumentClient)
        {
            FeedResponse<Dummy> response = CreateFeedResponse(expected);

            var mockDocumentQuery = new Mock<IFakeDocumentQuery<Dummy>>();
            mockDocumentQuery
                .SetupSequence(_ => _.HasMoreResults)
                .Returns(true)
                .Returns(false);

            mockDocumentQuery
                .Setup(_ => _.ExecuteNextAsync<Dummy>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var provider = new Mock<IQueryProvider>();
            provider
                .Setup(_ => _.CreateQuery<Dummy>(It.IsAny<Expression>()))
                .Returns(mockDocumentQuery.Object);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.Provider).Returns(provider.Object);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.Expression).Returns(dataSource.Expression);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.ElementType).Returns(dataSource.ElementType);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.GetEnumerator()).Returns(dataSource.GetEnumerator);


            mockDocumentClient.Setup(x => x.CreateDocumentQuery<Dummy>(It.IsAny<Uri>(),
                    It.IsAny<FeedOptions>()))
                .Returns(mockDocumentQuery.Object);

            var entityStore = new CosmosStore<Dummy>(new CosmonautClient(mockDocumentClient.Object), "databaseName", "", "http://test.com");
            return entityStore;
        }
        
        public static FeedResponse<T> CreateFeedResponse<T>(IQueryable<T> resource)
        {
            var feedResponseType = Type.GetType("Microsoft.Azure.Documents.Client.FeedResponse`1, Microsoft.Azure.DocumentDB.Core, Version=1.9.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var headers = new NameValueCollection
            {
                { "x-ms-request-charge", "0" },
                { "x-ms-activity-id", Guid.NewGuid().ToString() }
            };


            var arguments = new object[] { resource, resource.Count(), headers, false, null };

            var t = feedResponseType.MakeGenericType(typeof(T));

            var feedResponse = Activator.CreateInstance(t, flags, null, arguments, null);

            return (FeedResponse<T>) feedResponse;
        }

        public static CosmosStore<Dummy> ResponseSetupForQuery<T>(string sql, SqlParameterCollection parameters, IQueryable<T> expected, IQueryable<Dummy> dataSource, ref Mock<IDocumentClient> mockDocumentClient)
        {
            FeedResponse<T> response = CreateFeedResponse(expected);

            var mockDocumentQuery = new Mock<IFakeDocumentQuery<T>>();
            mockDocumentQuery
                .SetupSequence(_ => _.HasMoreResults)
                .Returns(true)
                .Returns(false);

            mockDocumentQuery
                .Setup(_ => _.ExecuteNextAsync<T>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var provider = new Mock<IQueryProvider>();
            provider
                .Setup(_ => _.CreateQuery<T>(It.IsAny<Expression>()))
                .Returns(mockDocumentQuery.Object);

            mockDocumentClient.Setup(x => x.CreateDocumentQuery<T>(It.IsAny<Uri>(), It.Is<SqlQuerySpec>(query => query.QueryText == sql),
                    It.IsAny<FeedOptions>()))
                .Returns(mockDocumentQuery.Object);
            

            var entityStore = new CosmosStore<Dummy>(new CosmonautClient(mockDocumentClient.Object), "databaseName", "", "http://test.com");
            return entityStore;
        }

        public static CosmosStore<Dummy> ResponseSetupForQuery(string sql, object parameters, IQueryable<Dummy> expected, IQueryable<Dummy> dataSource, ref Mock<IDocumentClient> mockDocumentClient)
        {
            FeedResponse<Dummy> response = CreateFeedResponse(expected);

            var mockDocumentQuery = new Mock<IFakeDocumentQuery<Dummy>>();
            mockDocumentQuery
                .SetupSequence(_ => _.HasMoreResults)
                .Returns(true)
                .Returns(false);

            mockDocumentQuery
                .Setup(_ => _.ExecuteNextAsync<Dummy>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var provider = new Mock<IQueryProvider>();
            provider
                .Setup(_ => _.CreateQuery<Dummy>(It.IsAny<Expression>()))
                .Returns(mockDocumentQuery.Object);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.Provider).Returns(provider.Object);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.Expression).Returns(dataSource.Expression);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.ElementType).Returns(dataSource.ElementType);
            mockDocumentQuery.As<IQueryable<Dummy>>().Setup(x => x.GetEnumerator()).Returns(dataSource.GetEnumerator);

            if (parameters == null)
            {
                mockDocumentClient.Setup(x => x.CreateDocumentQuery<Dummy>(It.IsAny<Uri>(), It.Is<SqlQuerySpec>(query => query.QueryText == sql),
                        It.IsAny<FeedOptions>()))
                    .Returns(mockDocumentQuery.Object);
            }
            else
            {
                var sqlParameters = parameters.ConvertToSqlParameterCollection();
                mockDocumentClient.Setup(x => x.CreateDocumentQuery<Dummy>(It.IsAny<Uri>(), 
                        It.Is<SqlQuerySpec>(query => query.QueryText == sql && query.Parameters == sqlParameters),
                        It.IsAny<FeedOptions>()))
                    .Returns(mockDocumentQuery.Object);
            }
            

            var entityStore = new CosmosStore<Dummy>(new CosmonautClient(mockDocumentClient.Object), "databaseName", "", "http://test.com");
            return entityStore;
        }
    }
}