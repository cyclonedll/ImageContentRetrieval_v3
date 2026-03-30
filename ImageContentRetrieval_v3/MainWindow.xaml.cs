using ImageContentRetrieval_v3.QuiverDb;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Vorcyc.Quiver;
using Vorcyc.RoundUI.Windows.Controls;

namespace ImageContentRetrieval_v3;

public partial class MainWindow : RoundNormalWindow
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += MainWindow_Loaded;
    }

    //private Ash2 _ash;
    private ImageDbContext _db = new(IOHelper.GetFileAbsolutePath("features.vdb"));
    private FeatureExtractor _featureExtractor;
    private Classifier _classifier;

    private System.Threading.SynchronizationContext _syncContext;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _syncContext = System.Threading.SynchronizationContext.Current;

        _featureExtractor = new FeatureExtractor();
        _classifier = new Classifier();

        this.IsEnabled = false;
        processBar1.Visibility = Visibility.Collapsed;

        try
        {

            await _db.LoadAsync();

            this.IsEnabled = true;
            _featureExtractor.GetFeaturesStarted += FeatureExtractor_GetFeaturesStarted;
            _featureExtractor.GetFeaturesProgressChanged += FeatureExtractor_GetFeaturesProgressChanged;

            lblInfo.Content = $"已建模 {_db.Images.Count} 个图像文件";
        }
        catch (Exception)
        {
            MessageBox.Show("特征文件加载失败");
            Application.Current.Shutdown();
        }
    }

    private void FeatureExtractor_GetFeaturesStarted(int obj)
    {
        processBar1.Maximum = obj;
    }

    private void FeatureExtractor_GetFeaturesProgressChanged(int index, int count)
    {
        if (_syncContext is null) return;
        _syncContext.Post(state =>
        {
            processBar1.Value = index;
            lblInfo.Content = $"正在对新图像文件建模：({index}/{count})";
        }, null);
    }




    private async void btnBuild_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            this.IsEnabled = btnCleanup.IsEnabled = btnBuild.IsEnabled = btnRetrieval.IsEnabled = false;
            processBar1.Visibility = Visibility.Visible;


            var sw = System.Diagnostics.Stopwatch.StartNew();

            var folder = dialog.FileName;
            var files = Directory.EnumerateFiles(folder, "*.jpg", SearchOption.AllDirectories);
            files = files.Concat(Directory.EnumerateFiles(folder, "*.jpeg", SearchOption.AllDirectories));
            files = files.Concat(Directory.EnumerateFiles(folder, "*.jfif", SearchOption.AllDirectories));
            files = files.Concat(Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories));



            files = IOHelper.Except(files, _db.Images);//ASH文件中已经有的，就排除；不须不再提取特征，否则非常耗时

            //var features = await _featureExtractor.ExtractFeaturesAsync(files);
            //if (features is not null)
            //{
            //    var set = FeaturesToImageDbSet(features);
            //    _db.Images.AddRange(set);
            //    await _db.SaveChangesAsync();
            //}
            var totalCount = files.Count();
            int successCount = 0;
            int index = 0;

            processBar1.Maximum = totalCount;

            await Task.Run(() =>
            {
                foreach (var file in files)
                {

                    var classification = _classifier.Classify(file, return_count: 1);
                    var embedding = _featureExtractor.ExtractFeature(file);

                    var finnalClassification = classification is null ? null : classification.ElementAt(0).tag;
                    var finnalEmbedding = embedding is null ? null : embedding;

                    var imageDb = new ImageDb
                    {
                        Filename = file,
                        ImageFeature = finnalEmbedding,
                        Classification = finnalClassification
                    };
                    _db.Images.Add(imageDb);
                    successCount++;

                    index++;
                    _syncContext.Post(state =>
                    {
                        processBar1.Value = index;
                        lblInfo.Content = $"正在对新图像文件建模：({index}/{totalCount})";
                    }, null);
                }
            });

            await _db.SaveChangesAsync();


            sw.Stop();

            lblInfo.Content = $"已建模 {_db.Images.Count} 个图像文件";

            var newCount = successCount;
            Vorcyc.RoundUI.Windows.Controls.ModernDialog.ShowMessage($"成功对 {successCount} 个文件建库，耗时 {sw.Elapsed}", "建库完成", MessageBoxButton.OK);

            this.IsEnabled = btnCleanup.IsEnabled = btnBuild.IsEnabled = btnRetrieval.IsEnabled = true;
            processBar1.Visibility = Visibility.Collapsed;

        }
    }

    private static IEnumerable<ImageDb> FeaturesToImageDbSet(IEnumerable<(string filename, float[] embedding)> features)
    {
        foreach (var feature in features)
        {
            yield return new ImageDb
            {
                Filename = feature.filename,
                ImageFeature = feature.embedding
            };
        }
    }

    private async void btnRetrieval_Click(object sender, RoutedEventArgs e)
    {

        //if (!System.IO.File.Exists(_ashFile))
        if (_db.Images.Count == 0)
        {
            MessageBox.Show("特征库无有效项，请先建库！");
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "JPG图像|*.jpg;*.jpeg;*.jfif;*.png|所有文件|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            //var sw = System.Diagnostics.Stopwatch.StartNew();

            var query_feature = _featureExtractor.ExtractFeature(ofd.FileName);

            //sw.Stop();
            //MessageBox.Show("提单张图片特征耗时 ：" + sw.Elapsed);
            //sw.Restart();

            if (!int.TryParse(cbReturnCount.Text, out int return_count))
            {
                MessageBox.Show("请输入有效数字！");
                return;
            }

            //var result =  _ash.Retrieval(target_feature, return_count);
            var result = await _db.Images.SearchAsync(e => e.ImageFeature, query_feature, return_count, default);

            //sw.Stop();

            //MessageBox.Show("检索耗时 : " + sw.Elapsed);


            dg1.ItemsSource = result; //RetrievalResult.ConvertFromVauleTuple(result);


            //这样做就可以在打开的时候也删文件了
            imgSource.Source = ReadImage(ofd.FileName);
        }
    }




    private void dg1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dg1.SelectedItem != null)
        {
            var row = (QuiverSearchResult<ImageDb>)dg1.SelectedItem;
            imgSelected.Source = ReadImage(row.Entity.Filename);
        }
    }


    private void dg1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dg1.SelectedItem == null) return;
        var row = (QuiverSearchResult<ImageDb>)dg1.SelectedItem;
        ShellFolderSelector.LocateFile(row.Entity.Filename);
    }


    private async void btnCleanup_Click(object sender, RoutedEventArgs e)
    {
        this.IsEnabled = btnCleanup.IsEnabled = btnBuild.IsEnabled = btnRetrieval.IsEnabled = false;

        await IOHelper.CleanupAsync(_db);

        lblInfo.Content = $"已建模 {_db.Images.Count} 个图像文件";
        this.IsEnabled = btnCleanup.IsEnabled = btnRetrieval.IsEnabled = btnBuild.IsEnabled = true;
    }



    #region Helper Methods
    private static BitmapImage ReadImage(string imgFilename)
    {
        var imageData = File.ReadAllBytes(imgFilename);
        using var imageStream = new MemoryStream(imageData);
        BitmapImage bi = new BitmapImage();
        // BitmapImage.UriSource must be in a BeginInit/EndInit block.
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;//设置缓存模式
        bi.StreamSource = imageStream;
        bi.EndInit();
        return bi;
    }



    #endregion


}
