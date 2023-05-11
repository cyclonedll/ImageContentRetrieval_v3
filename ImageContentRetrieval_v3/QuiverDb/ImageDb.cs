using Vorcyc.Quiver;

namespace ImageContentRetrieval_v3.QuiverDb;

public class ImageDb
{

    [QuiverKey]
    public string Filename { get; set; } = string.Empty;

    [QuiverVector(1001, DistanceMetric.Euclidean)]
    public float[] ImageFeature { get; set; } = [];

}
