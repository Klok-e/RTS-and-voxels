using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Scripts.World.Components
{
    [Serializable]
    public struct MapLoader : IComponentData
    {
        public int ChunkDistance;
    }

    public class MapLoaderProxy : ComponentDataProxy<MapLoader>
    {
    }
}
