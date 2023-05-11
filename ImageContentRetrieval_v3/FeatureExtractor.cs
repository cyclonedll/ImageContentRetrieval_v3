namespace ImageContentRetrieval_v3;

/*
* 2022‎年‎7‎月‎20‎日，‏‎0:43 cyclone_dll 于昆明创建
* 这是对去年同期的改进
* ：
* 1.   用了新的 inception_v3_2016_08_28_frozen.pb 模型
* 2.   现在返回向量长度为1001，老的是2048；于是特征向量的RAM占用和存储的磁盘占用都少了超过60%
* 3.   由于新的模型不支持图片解码，现在用外部的图像解码。现在支持JPEG：jpeg、jpg、jfif，PNG
* 4.   由于改成实例方法，现在单次提取速度会变快
*/

using NumSharp;
using Tensorflow;
//using Tensorflow.NumPy;
using static Tensorflow.Binding;


internal class FeatureExtractor : IDisposable
{


    //老的返回长为    :   2048的向量
    //现在的返回长为  :   1001


    const string dir = "label_image_data";
    const string pbFile = "inception_v3_2016_08_28_frozen.pb";
    const string labelFile = "imagenet_slim_labels.txt";

    private Session? _session;
    private Graph? _graph;
    private Tensor? _bottleneck, _input_image;


    public FeatureExtractor()
    {
        tf.compat.v1.disable_eager_execution();

        import_graph();
    }

    //因为新模型只有个占位符，没有jpeg解码器
    //所以要单独解码，然后传给占位符！
    //但是示例中的 graph.get_tensor_by_name(input_name); 代码却有问题
    private void import_graph()
    {
        _graph = new Graph();
        _graph.Import(pbFile);

        //foreach (var n in graphDef.Node)
        //{
        //    Debug.WriteLine(n);
        //}

        //_input_image = _graph.OperationByName("input");// graph.get_tensor_by_name(input_name);
        //_bottleneck = _graph.OperationByName("InceptionV3/Predictions/Reshape");// graph.get_operation_by_name(output_name);
        // or
        //下面是以前老的，也是我喜欢的写法
        var return_tensors = tf.import_graph_def(
            _graph.as_graph_def(),
            name: "",
            return_elements: new[]
            {
                    "input:0",
                    "InceptionV3/Predictions/Reshape:0"
            })
            .Select(x => x as Tensor)
            .ToArray();

        _input_image = return_tensors[0];
        _bottleneck = return_tensors[1];


        var config = new ConfigProto
        {
            AllowSoftPlacement = true,
            GpuOptions = new GPUOptions
            {
                AllowGrowth = true,
                ForceGpuCompatible = true
            }
        };

        _session = new Session(_graph, config);
        //or
        //_session = tf.Session(_graph, config);
    }


    private static NDArray ReadTensorFromImageFile(string file_name,
                      int input_height = 299,
                      int input_width = 299,
                      int input_mean = 0,
                      int input_std = 255)
    {
        var graph = tf.Graph().as_default();

        var file_reader = tf.io.read_file(file_name, "file_reader");
        //var image_reader = tf.image.decode_jpeg(file_reader, channels: 3, name: "jpeg_reader");
        //现在！！！！可以通过这种方式读取不止  JPEG格式 的图片了
        var image_reader = tf.image.decode_image(file_reader, channels: 3, name: "image_reader");
        var caster = tf.cast(image_reader, tf.float32);
        var dims_expander = tf.expand_dims(caster, 0);
        var resize = tf.constant(new int[] { input_height, input_width });
        var bilinear = tf.image.resize_bilinear(dims_expander, resize);
        var sub = tf.subtract(bilinear, new float[] { input_mean });
        var normalized = tf.divide(sub, new float[] { input_std });

        using var sess = tf.Session(graph);
        return sess.run(normalized);
    }

    //老的返回长为    :   2048的向量
    //现在的返回长为  :   1001
    private NDArray? get_bottleneck_data(
        string image_file,
        int input_width = 299, int input_height = 299, int input_mean = 0, int input_std = 255)
    {
        try
        {
            var nd = ReadTensorFromImageFile(image_file,
                                  input_height: input_height,
                                  input_width: input_width,
                                  input_mean: input_mean,
                                  input_std: input_std);

            var bottleneck_data = _session.run(_bottleneck.outputs[0], new FeedItem(_input_image.outputs[0], nd));
            bottleneck_data = np.squeeze(bottleneck_data);
            return bottleneck_data;
        }
        catch (Exception)
        {
            return null;
        }
    }


    public float[]? ExtractFeature(string image_file, int input_width = 299, int input_height = 299, int input_mean = 0, int input_std = 255)
        => get_bottleneck_data(image_file, input_width, input_height, input_mean, input_std)?.ToArray<float>();






    public IEnumerable<(string, float[])> ExtractFeatures(
        IEnumerable<string> imageFilenames,
        int input_width = 299, int input_height = 299, int input_mean = 0, int input_std = 255)
    {

        if (imageFilenames == null)
            throw new NullReferenceException("imageFilenames");

        var resultFeatures = new List<(string, float[])>();

        foreach (var img in imageFilenames)
        {

            var feature = get_bottleneck_data(img, input_width, input_height, input_mean, input_std);

            if (feature == null)
                continue;

            resultFeatures.Add((img, feature.ToArray<float>()));
        }

        return resultFeatures;
    }


    private int _index = 0;
    public async Task<IEnumerable<(string filename, float[] feature)>> ExtractFeaturesAsync(
        IEnumerable<string> imageFilenames,
        int input_width = 299, int input_height = 299, int input_mean = 0, int input_std = 255)
    {

        if (imageFilenames == null)
            throw new NullReferenceException("imageFilenames");

        var count = imageFilenames.Count();
        Raise_GetFeaturesStared(count);
        _index = 0;

        return await Task.Run(() =>
        {

            var resultFeatures = new List<(string, float[])>();

            foreach (var img in imageFilenames)
            {

                var feature = get_bottleneck_data(img, input_width, input_height, input_mean, input_std);

                if (feature == null)
                {
                    _index++;
                    Raise_GetFeaturesProgressChanged(_index, count);
                    continue;
                }

                resultFeatures.Add((img, feature.ToArray<float>()));
                _index++;
                Raise_GetFeaturesProgressChanged(_index, count);
            }

            return resultFeatures;

        });
    }


    #region events

    /// <summary>
    /// 值为图片数量
    /// </summary>
    public event Action<int> GetFeaturesStarted;

    /// <summary>
    /// 值为已处理数
    /// </summary>
    public event Action<int, int> GetFeaturesProgressChanged;


    private void Raise_GetFeaturesStared(int count)
        => GetFeaturesStarted?.Invoke(count);

    private void Raise_GetFeaturesProgressChanged(int index, int count)
        => GetFeaturesProgressChanged?.Invoke(index, count);


    #endregion




    #region dispost pattern

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)                    
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _session.Dispose();
            _graph.Dispose();
            _input_image.Dispose();
            _bottleneck.Dispose();

            _session = null;
            _graph = null;
            _input_image = null;
            _bottleneck = null;

            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~x()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion

}
