using System;

namespace CarRapide.World.Osm
{
    [Serializable]
    public sealed class OsmResponse
    {
        public OsmElement[] elements;
    }

    [Serializable]
    public sealed class OsmElement
    {
        public string type;
        public long id;
        public double lat;
        public double lon;
        public OsmTags tags;
        public OsmPoint[] geometry;
    }

    [Serializable]
    public sealed class OsmPoint
    {
        public double lat;
        public double lon;
    }

    [Serializable]
    public sealed class OsmTags
    {
        public string highway;
        public string building;
        public string name;
        public string amenity;
        public string surface;
        public string lanes;
        public string oneway;
    }
}
