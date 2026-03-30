using NumSharp;
using Tensorflow;
using static Tensorflow.Binding;

namespace ImageContentRetrieval_v3;


internal class Classifier : IDisposable
{

    const string dir = "label_image_data";
    const string pbFile = "inception_v3_2016_08_28_frozen.pb";
    const string labelFile = "imagenet_slim_labels_combined.csv";


    private Session? _session;
    private Graph? _graph;
    private Tensor? _reshape_1, _input_image;

    private string[]? _labels;

    public Classifier()
    {
        _labels = ReadLabels();

        tf.compat.v1.disable_eager_execution();

        import_graph();
    }


    //因为新模型只有个占位符，没有jpeg解码器
    //所以要单独解码，然后传给占位符！
    //但是示例中的 graph.get_tensor_by_name(input_name); 代码却有问题

    private void import_graph()
    {
        _graph = new Graph();
        _graph.Import(Path.Join(dir, pbFile));

        //foreach (var n in graphDef.Node)
        //{
        //    Debug.WriteLine(n);
        //}

        _input_image = _graph.OperationByName("input");// graph.get_tensor_by_name(input_name);
        _reshape_1 = _graph.OperationByName("InceptionV3/Predictions/Reshape_1");// graph.get_operation_by_name(output_name);

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


    private NDArray? get_output_data(
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

            var bottleneck_data = _session.run(_reshape_1.outputs[0], new FeedItem(_input_image.outputs[0], nd));
            bottleneck_data = np.squeeze(bottleneck_data);
            return bottleneck_data;
        }
        catch (Exception)
        {
            return null;
        }
    }



    public IEnumerable<(int class_index, string tag, float score)>?
        Classify(string image_file, int return_count = 5, float? threshold = null,
        int input_width = 299, int input_height = 299, int input_mean = 0, int input_std = 255)
    {
        if (!File.Exists(image_file))
            throw new FileNotFoundException(image_file);

        try
        {

            NDArray graph_results = get_output_data(image_file, input_width, input_height, input_mean, input_std);

            if (graph_results is null) return null;

            NDArray? argsort = np.argsort<int>(graph_results);//按置信度排序


            var top_k = argsort.ToArray<int>()
                .Skip(graph_results.size - return_count)
                .Reverse()
                .ToArray();

            //foreach (float idx in top_k)
            //    Console.WriteLine($"{picFile}: {idx} {labels[(int)idx]}, {graph_results[(int)idx]}");

            var result = new List<(int, string, float)>();
            foreach (var idx in top_k)
            {
                if (threshold == null)
                    result.Add((idx, _labels[idx], graph_results[idx]));
                else
                    if (graph_results[idx] >= threshold)
                        result.Add((idx, _labels[idx], graph_results[idx]));
            }


            return result;

        }
        catch (Exception)
        {
            return null;
        }

    }


    private static string[] ReadLabels()
    {
        return File.ReadAllLines(Path.Join(dir, labelFile), System.Text.Encoding.UTF8);
    }


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
            _reshape_1.Dispose();

            _session = null;
            _graph = null;
            _input_image = null;
            _reshape_1 = null;

            _labels = null;
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
