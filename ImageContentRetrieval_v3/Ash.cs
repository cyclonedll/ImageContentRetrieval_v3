using System.Printing;
using System.Text;

namespace ImageContentRetrieval_v3;

/* ‎2021‎年‎7‎月‎17‎日，‏‎13:40:05 cyclone_dll 于昆明创建
* 解决特征存储和检索问题
*
*
* 结构如下：
* 字符串长度， 字符串值（字节）,    特征序列（数组）
* int32       byte[] UTF-16编码 single[]
*
*/

/*
 * ‎2022‎年‎8‎月‎11‎日，‏‎13:20 cyclone_dll 于昆明修改
 */

internal class Ash2
{

    public enum AshFileMode
    {
        Create,
        Open,
    }

    private Dictionary<string, float[]> _features = new();

    private int _featureSize;//表示特征向量的长度 , 其为float的数组。长度可以自定义

    private string _currentAshFile;//表示当前打开的ASH文件

    private AshFileMode _mode;


    public Ash2(int featureSize, string ashFilename)
    {
        _featureSize = featureSize;
        _currentAshFile = ashFilename;
    }



    /// <summary>
    /// 在构造实例后调用此方法
    /// </summary>
    /// <returns></returns>
    public async Task InitAsync()
    {
        await Task.Run(() =>
        {
            if (File.Exists(_currentAshFile))
            {
                _features.Clear();//清空

                using var fs = new FileStream(_currentAshFile, FileMode.Open);
                using var br = new BinaryReader(fs);

                _featureSize = br.ReadInt32();

                while (fs.Position < fs.Length)
                {
                    /*1 先读文件名*/
                    var bufferLen = br.ReadInt32();
                    var buffer = br.ReadBytes(bufferLen);
                    var filename = Encoding.Unicode.GetString(buffer);

                    /*2 再读特征组*/
                    float[] feature = new float[_featureSize];
                    for (int i = 0; i < _featureSize; i++)
                    {
                        feature[i] = br.ReadSingle();
                    }

                    _features.Add(filename, feature);
                }

                _mode = AshFileMode.Open;
            }
            else
                _mode = AshFileMode.Create;

        });
    }




    /// <summary>
    /// 将当前Ash实例中的全部特征写入到 <see cref="CurrentAshFile"/> 文件。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public async Task BuildAsync()
    {
        if (_features is null)
            throw new ArgumentNullException(nameof(_features));

        if (_currentAshFile is null)
            throw new ArgumentNullException(nameof(_currentAshFile));


        await Task.Run(() =>
        {
            using var fs = new FileStream(_currentAshFile, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            bw.Write(_featureSize);//特征大小

            foreach (var f in _features)
            {
                if (f.Value.Length != _featureSize)
                    throw new ArgumentException($"Some feature size of item is not {_featureSize}");

                /*1 先写文件名*/
                //不能直接使用bw.Write(string)
                //因为当文件名是中文时就会出错
                //路径有中文不影响
                //或许是个BUG
                var buffer = Encoding.Unicode.GetBytes(f.Key);

                bw.Write(buffer.Length);
                bw.Write(buffer);

                /*2 再写特征组*/
                foreach (var featureItem in f.Value)
                {
                    bw.Write(featureItem);
                }
            }

        });


        //实例 instance
        //托管对象才可以自动释放 —— 被CLR管理，受GC约束, IDispose
        //非托管对象 

    }//fs.Dispose();






    /// <summary>
    /// 根据特征值检索当前ASH文件中特征值相近的结果。
    /// </summary>
    /// <param name="target">待检索的特征值</param>
    /// <param name="return_items">返回项目的数量，若超过当前ASH文件中的特征总数，则返回总数量</param>
    /// <returns></returns>
    public IEnumerable<(float similarity, int index, string filename)> Retrieval
        (float[] target, int return_items)
    {

        var distances = new List<(float, int, string)>();

        int index = 0;
        foreach (var feature in _features)
        {
            var distance = Euclidean.Similarity(target, feature.Value);
            distances.Add((distance, index, feature.Key));
            index++;
        }

        var actualTopItems = Math.Min(distances.Count, return_items);

        //------------ for Distance
        //距离最小的在最前
        //distances.Sort();
        //for (int i = 0; i < actualTopItems; i++)
        //{
        //    yield return distances[i];
        //}

        //------------- for Similarity
        var returnItems = distances.OrderByDescending(x => x.Item1).Take(actualTopItems);

        foreach (var item in returnItems)
            yield return item;
    }


    #region Add

    /// <summary>
    /// 向当前Ash实例中的特征字典添加文件名和它的特征值，如果当前ASH文件中已存在相同文件名，则不添加。
    /// </summary>
    /// <param name="filename">目标文件名，其作为特征字典的键。</param>
    /// <param name="feature">特征向量</param>
    /// <exception cref="ArgumentException"></exception>
    public void Add(string filename, float[] feature)
    {
        if (feature.Length != _featureSize)
            throw new ArgumentException($"The size of feature is not {_featureSize}");

        _features.TryAdd(filename, feature);
    }

    /// <summary>
    /// 添加多个文件名和它们对应的特征值，若当前ASH文件中已存在，则不添加。
    /// </summary>
    /// <param name="values">文件名和特征值对的集合</param>
    /// <exception cref="ArgumentException">特征向量长度不匹配时抛出</exception>
    public void AddRange(IEnumerable<(string filename, float[] feature)> values)
    {
        if (values.Any((f) => f.feature.Length != _featureSize))
            throw new ArgumentException($"The size of feature is not {_featureSize}");

        foreach (var value in values)
            _features.TryAdd(value.filename, value.feature);
    }

    #endregion


    /// <summary>
    ///  从当前ASH实例的特征字典中移除文件名为 <paramref name="filename"/> 的特征组。
    /// </summary>
    /// <param name="filename"></param>
    public void Remove(string filename) => _features.Remove(filename);


    /// <summary>
    /// Removes all items from Current Ash Features.
    /// 清空当前 ASH实例的特征字典库。
    /// </summary>
    public void Clear() => _features.Clear();


    /// <summary>
    ///  从当前特征字典中排除<paramref name="filenames"/>中出现的项，并返回新的序列.
    /// </summary>
    /// <param name="filenames"></param>
    /// <returns></returns>
    public IEnumerable<string> Except(IEnumerable<string> filenames)
    {
        foreach (var fn in filenames)
        {
            //若不存在则迭代返回
            if (!_features.ContainsKey(fn))
                yield return fn;
        }
    }

    /// <summary>
    /// 清理磁盘上已经不存在的但是 ASH 中存在的文件
    /// </summary>
    /// <returns></returns>
    public async Task CleanupAsync()
    {
        await Task.Run(async () =>
        {
            var files = from f in _features
                        select f.Key;

            foreach (var file in files)
                if (!File.Exists(file))
                    _features.Remove(file);

            File.Delete(_currentAshFile);
            await BuildAsync();
        });
    }


    #region Properties

    /// <summary>
    /// Gets the number of features in current ASH file.
    /// 返回当前特征字典的数量。
    /// </summary>
    public int Count => _features.Count;

    /// <summary>
    /// 返回特征向量的长度。
    /// </summary>
    public int FeatureSize => _featureSize;

    /// <summary>
    /// 返回当前对应的文件
    /// </summary>
    public string CurrentAshFile => _currentAshFile;

    /// <summary>
    /// 返回当前处于创建或是打开模式。
    /// </summary>
    public AshFileMode Mode => _mode;


    #endregion



}

