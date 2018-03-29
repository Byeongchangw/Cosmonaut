﻿using System.Collections.Generic;
using System.Linq;

namespace Cosmonaut.Response
{
    public class CosmosMultipleResponse<TEntity> where TEntity : class
    {
        public bool IsSuccess => !FailedEntities.Any();

        public List<CosmosResponse<TEntity>> FailedEntities { get; } = new List<CosmosResponse<TEntity>>();
    }
}