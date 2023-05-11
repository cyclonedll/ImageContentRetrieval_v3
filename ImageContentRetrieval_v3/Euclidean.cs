namespace ImageContentRetrieval_v3;

using static System.MathF;

public static class Euclidean
{


    public static float Distance(float x, float y)
    {
        return Abs(x - y);
    }



    public static float Distance(float[] x, float[] y)
    {
        float sum = 0.0f;
        for (int i = 0; i < x.Length; i++)
        {
            float u = x[i] - y[i];
            sum += u * u;
        }
        return Sqrt(sum);
    }



    public static float Distance(float vector1x, float vector1y, float vector2x, float vector2y)
    {
        float dx = vector1x - vector2x;
        float dy = vector1y - vector2y;
        return Sqrt(dx * dx + dy * dy);
    }


    public static float Similarity(float x, float y)
    {
        return 1.0f / (1.0f + Abs(x - y));
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    /// <remarks>
    /// The length of x must be equals to length of y.
    /// </remarks>
    public static float Similarity(float[] x, float[] y)
    {
        float sum = 0.0f;

        for (int i = 0; i < x.Length; i++)
        {
            float u = x[i] - y[i];
            sum += u * u;
        }

        return 1.0f / (1.0f + Sqrt(sum));
    }




}
