namespace ImageContentRetrieval_v3;

public class RetrievalResult
{
    public static IEnumerable<RetrievalResult> ConvertFromVauleTuple(
        IEnumerable<(float similarity, int index, string filename)> valueTuple_retrievalResults)
    {
        foreach (var retrievalResult in valueTuple_retrievalResults)
            yield return new RetrievalResult(retrievalResult);
    }

    public RetrievalResult((float similarity, int index, string filename) pack)
    {
        Similarity = $"{pack.similarity * 100:0.##} %";
        Index = pack.index;
        Filename = pack.filename;
    }

    public string Similarity { get; }
    public int Index { get; }
    public string Filename { get; }
}
