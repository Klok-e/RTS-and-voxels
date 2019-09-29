using System;
using Unity.Entities;

namespace World.Components
{
    [Serializable]
    public struct MapLoader : IComponentData
    {
        public int RegionDistance;
    }

    public class MapLoaderProxy : ComponentDataProxy<MapLoader>
    {
    }
}