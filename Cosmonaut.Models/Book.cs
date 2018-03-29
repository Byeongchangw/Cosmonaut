﻿using Cosmonaut.Attributes;
using Newtonsoft.Json;

namespace Cosmonaut.Models
{
    [CosmosCollection("TheBooks", 700)]
    public class Book : ICosmosEntity
    {
        public string Name { get; set; }

        public TestUser Author { get; set; }

        public string CosmosId { get; set; }
    }
}