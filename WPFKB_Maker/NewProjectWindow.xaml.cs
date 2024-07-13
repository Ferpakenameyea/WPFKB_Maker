using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPFKB_Maker.TFS;
using WPFKB_Maker.TFS.KBBeat;
using WPFKB_Maker.TFS.Sound;

namespace WPFKB_Maker
{
    /// <summary>
    /// NewProject.xaml 的交互逻辑
    /// </summary>
    public partial class NewProjectWindow : Window
    {
        public string SelectedFilePath { get; private set; }
        public string SavingFilePath { get; private set; }
        private readonly MetaBuilder metaBuilder = new MetaBuilder();

        private readonly ObservableCollection<string> levelAuthors = new ObservableCollection<string>();
        private readonly ObservableCollection<string> composers = new ObservableCollection<string>();

        private readonly Regex regex = new Regex("^[a-zA-Z_0-9]+[.][a-zA-Z_0-9]+$");
        private readonly MainWindow mainWindow;

        public NewProjectWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            levelAuthorsListView.ItemsSource = this.levelAuthors;
            composersListView.ItemsSource = this.composers;
            this.remindingBox.Text = $"KBMaker WPF {TFS.Version.version}";
            this.mainWindow = mainWindow;
        }

        private void BrowseLocalFile(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "AudioFiles (*.mp3, *.wav)|*.mp3;*.wav"
            };
            if (dialog.ShowDialog() == true)
            {
                this.SelectedFilePath = dialog.FileName;
                this.selectedFileNameBox.Text = new FileInfo(this.SelectedFilePath).Name;
            }
        }

        private async void BPMButtonDown(object sender, RoutedEventArgs e)
        {
            if (SelectedFilePath == null)
            {
                MessageBox.Show("您还没有选择歌曲文件！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                this.measureBPMButton.IsEnabled = false;
                this.measureBPMButton.Content = "测量中……";
                var bpm = await new BPMGetter(this.SelectedFilePath).Run();
                this.measureBPMButton.IsEnabled = true;
                this.measureBPMButton.Content = "自动测量（实验性）";

                var result = MessageBox.Show($"自动检测的BPM为：{bpm}，要将其应用吗？", "检测", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    this.bpmBox.Text = bpm.ToString();
                }
            }
            catch (Exception err)
            {
                MessageBox.Show($"获取BPM时发生错误：{err}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckFloat(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;

            if (float.TryParse(textBox.Text, out var value) == false || value <= 0)
            {
                textBox.Text = string.Empty;
                remindingBox.Text = "不可接受的BPM，请输入正实数。";
            }
        }

        private void AddLevelAuthor(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.levelAuthorInputField.Text) || 
                this.levelAuthors.Contains(this.levelAuthorInputField.Text))
            {
                return;
            }

            this.levelAuthors.Add(this.levelAuthorInputField.Text);
            
            this.levelAuthorInputField.Text = string.Empty;
        }

        private void RemoveLevelAuthor(object sender, RoutedEventArgs e)
        {
            this.levelAuthors.Remove(this.levelAuthorsListView.SelectedItem as string);
        }

        private void AddComposer(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.composerInputField.Text) ||
                this.composers.Contains(this.composerInputField.Text))
            {
                return;
            }

            this.composers.Add(this.composerInputField.Text);

            this.composerInputField.Text = string.Empty;
        }

        private void RemoveComposer(object sender, RoutedEventArgs e)
        {
            this.composers.Remove(this.composersListView.SelectedItem as string);
        }

        private void AssetBundleNameCheck(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!regex.Match(textBox.Text).Success)
            {
                textBox.Text = string.Empty;
                this.remindingBox.Text = "不合法的AB包名称，名称为<父包名称>.<子包名称>，包名称由英文字母，数字与下划线组成。";
            }
        }

        private void DifficultyCheck(object sender, RoutedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (!int.TryParse(textbox.Text, out var difficulty) || difficulty <= 0)
            {
                this.remindingBox.Text = "不合法的难度系数，难度是一个大于零的整数。";
                textbox.Text = string.Empty;
            }
        }

        private void TrackSizeCheck(object sender, RoutedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (!int.TryParse(textbox.Text, out var size) || size <= 0 || size > 5)
            {
                this.remindingBox.Text = "不合法的轨道大小，轨道大小是一个1~5的整数。";
                textbox.Text = string.Empty;
            }
        }

        private void VectorValueCheck(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!float.TryParse(textBox.Text, out var value))
            {
                this.remindingBox.Text = "不合法的向量分量值，向量分量是一个合法小数。";
                textBox.Text = string.Empty;
            }
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BuildMeta(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            button.IsEnabled = false;

            try
            {
                if (this.SelectedFilePath == null)
                {
                    throw new ArgumentNullException("音乐文件");
                }
                if (this.SavingFilePath == null)
                {
                    throw new ArgumentNullException("保存路径");
                }

                byte[] data = Array.Empty<byte>();
                WaveFormat waveFormat = null;
                string extensionName = new FileInfo(this.SelectedFilePath).Extension;
                button.Content = "正在读取音乐文件……";
                await Task.Run(() =>
                {
                    data = File.ReadAllBytes(this.SelectedFilePath);
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        using (var reader = KBMakerWaveStream.GetWaveStream(this.SelectedFilePath))
                        {
                            waveFormat = reader.WaveFormat;
                        }
                    }
                }).ConfigureAwait(true);
                
                button.Content = "正在构建项目……";

                var meta = this.metaBuilder
                    .SetAssetBundleName(this.assetBundleBox.Text)
                    .SetName(this.musicTitleBox.Text)
                    .SetDescription(this.musicSubtitleBox.Text)
                    .SetDifficulty(int.Parse(this.difficultyBlock.Text))
                    .SetLeftTrackSize(int.Parse(this.leftTrackSizeBlock.Text))
                    .SetRightTrackSize(int.Parse(this.rightTrackSizeBlock.Text))
                    .SetLevelAuthors(this.levelAuthors.ToList())
                    .SetComposers(this.composers.ToList())
                    .SetBpm(float.Parse(this.bpmBox.Text))
                    .SetNoteAppearPosition(
                        new TFS.KBBeat.Unity.UnityVector3(
                            float.Parse(vectorValueXBox.Text),
                            float.Parse(vectorValueYBox.Text),
                            float.Parse(vectorValueZBox.Text)
                        )
                    )
                    .SetMusicFile(data)
                    .SetWaveFormat(waveFormat)
                    .SetExtensionName(extensionName)
                    .Build();

                Project.Current = new Project(meta, new HashSheet(
                    meta.LeftTrackSize + meta.RightTrackSize,
                    meta.LeftTrackSize,
                    meta.RightTrackSize), this.savingPathBlock.Text);
                
                mainWindow.SheetPlayer.Project = Project.Current;
                mainWindow.SheetEditor.Project = Project.Current;

                this.Close();
            }
            catch (Exception err)
            {
                button.IsEnabled = true;
                button.Content = "创建项目";

                if (err is FormatException)
                {
                    this.remindingBox.Text = "数字参数未正确填写。";
                }
                else
                {
                    this.remindingBox.Text = err.Message;
                }
                return;
            }
        }

        private void BrowseSavePathButtonDown(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save project",
                Filter = "KBBeat wpf project (*.kbpwpf)|*.kbpwpf",
                FileName = "新建项目.kbpwpf"
            };
            var result = saveFileDialog.ShowDialog(this);
            if (result == true)
            {
                savingPathBlock.Text = saveFileDialog.FileName;
                SavingFilePath = saveFileDialog.FileName;
            }
        }

        private void OpenExternalTool(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("是否打开外部工具下载页面？", "外部工具", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            string url = "https://mixmeister-bpm-analyzer.en.softonic.com/";
            Process.Start(new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
    }
}
