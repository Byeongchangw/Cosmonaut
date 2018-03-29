﻿using Cosmonaut.Attributes;
using Newtonsoft.Json;

namespace Cosmonaut.Models
{
    [CosmosCollection(Throughput = 1000)]
    public class Book : ICosmosEntity
    {
        public string Name { get; set; }

        public TestUser Author { get; set; }

        public string CosmosId { get; set; }
    }
}