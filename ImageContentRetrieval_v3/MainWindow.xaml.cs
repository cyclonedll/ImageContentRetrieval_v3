using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Vorcyc.RoundUI.Windows.Controls;

namespace ImageContentRetrieval_v3;

public partial class MainWindow : RoundNormalWindow
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += MainWindow_Loaded;
    }

    private Ash2 _ash = new(1001, GetFileAbsolutePath("features.ash"));
    private FeatureExtractor _featureExtractor;


    private System.Threading.SynchronizationContext _syncContext;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _syncContext = System.Threading.SynchronizationContext.Current;

        _featureExtractor = new FeatureExtractor();

        this.IsEnabled = false;
        processBar1.Visibility = Visibility.Collapsed;

        try
        {
            await _ash.InitAsync();

            this.IsEnabled = true;
            _featureExtractor.GetFeaturesStarted += FeatureExtractor_GetFeaturesStarted;
            _featureExtractor.GetFeaturesProgressChanged += FeatureExtractor_GetFeaturesProgressChanged;

            lblInfo.Content = $"已建模 {_ash.Count} 个图像文件";
            //MessageBox.Show("loaded done");
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



            files = _ash.Except(files);//ASH文件中已经有的，就排除；不须不再提取特征，否则非常耗时
            var features = await _featureExtractor.ExtractFeaturesAsync(files);
            _ash.AddRange(features);
            await _ash.BuildAsync();


            sw.Stop();

            lblInfo.Content = $"已建模 {_ash.Count} 个图像文件";

            MessageBox.Show($"对 {features.Count()} 个文件建库，耗时 {sw.Elapsed}");

            this.IsEnabled = btnCleanup.IsEnabled = btnBuild.IsEnabled = btnRetrieval.IsEnabled = true;
            processBar1.Visibility = Visibility.Collapsed;

        }
    }


    private void btnRetrieval_Click(object sender, RoutedEventArgs e)
    {

        //if (!System.IO.File.Exists(_ashFile))
        if (_ash is null)
        {
            MessageBox.Show("特征库文件不存在，请先建库！");
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "JPG图像|*.jpg;*.jpeg;*.jfif;*.png|所有文件|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            //var sw = System.Diagnostics.Stopwatch.StartNew();

            var target_feature = _featureExtractor.ExtractFeature(ofd.FileName);

            //sw.Stop();
            //MessageBox.Show("提单张图片特征耗时 ：" + sw.Elapsed);
            //sw.Restart();

            if (!int.TryParse(cbReturnCount.Text, out int return_count))
            {
                MessageBox.Show("请输入有效数字！");
                return;
            }

            var result = _ash.Retrieval(target_feature, return_count);

            //sw.Stop();

            //MessageBox.Show("检索耗时 : " + sw.Elapsed);


            dg1.ItemsSource = RetrievalResult.ConvertFromVauleTuple(result);


            //这样做就可以在打开的时候也删文件了
            imgSource.Source = ReadImage(ofd.FileName);
        }
    }




    private void dg1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dg1.SelectedItem != null)
        {
            var row = (RetrievalResult)dg1.SelectedItem;
            imgSelected.Source = ReadImage(row.Filename);
        }
    }


    private void dg1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dg1.SelectedItem == null) return;
        var row = (RetrievalResult)dg1.SelectedItem;
        ShellFolderSelector.LocateFile(row.Filename);
    }




    private async void btnCleanup_Click(object sender, RoutedEventArgs e)
    {
        this.IsEnabled = btnCleanup.IsEnabled = btnBuild.IsEnabled = btnRetrieval.IsEnabled = false;

        await _ash.CleanupAsync();

        lblInfo.Content = $"已建模 {_ash.Count} 个图像文件";
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


    public static string GetExecutionDirectory()
    {
        return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }

    public static string GetFileAbsolutePath(string filename)
    {
        return System.IO.Path.Combine(GetExecutionDirectory(), filename);
    }


    #endregion


}
