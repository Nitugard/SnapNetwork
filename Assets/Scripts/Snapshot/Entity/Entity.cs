using System;

namespace Ibc.Survival
{
    /// <summary>
    /// Entity is network object abstraction.
    /// </summary>
    [Serializable]
    public struct Entity
    {
        /// <summary>
        /// Unique entity identifier across network.
        /// </summary>
        public EntityId Id;

        /// <summary>
        /// Prefab identifier.
        /// </summary>
        public PrefabId PrefabId;

    }
}
