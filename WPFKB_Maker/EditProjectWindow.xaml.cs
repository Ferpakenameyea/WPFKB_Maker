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
    public partial class EditProjectWindow : Window
    {
        private readonly MetaBuilder metaBuilder = new MetaBuilder();

        private readonly ObservableCollection<string> levelAuthors = new ObservableCollection<string>();
        private readonly ObservableCollection<string> composers = new ObservableCollection<string>();

        private readonly Regex regex = new Regex("^[a-zA-Z_0-9]+[.][a-zA-Z_0-9]+$");
        private readonly MainWindow mainWindow;
        private readonly Project editTarget;

        public EditProjectWindow(MainWindow mainWindow, Project project)
        {
            InitializeComponent();

            levelAuthorsListView.ItemsSource = this.levelAuthors;
            composersListView.ItemsSource = this.composers;
            this.remindingBox.Text = $"KBMaker WPF {TFS.Version.version}";
            this.mainWindow = mainWindow;
            this.editTarget = project;

            FillParameters();
        }

        private void FillParameters()
        {
            this.assetBundleBox.Text = editTarget.Meta.AssetBundleName;
            this.musicTitleBox.Text = editTarget.Meta.Name;
            this.musicSubtitleBox.Text = editTarget.Meta.Description;
            this.difficultyBlock.Text = editTarget.Meta.Difficulty.ToString();
            this.leftTrackSizeBlock.Text = editTarget.Meta.LeftTrackSize.ToString();
            this.rightTrackSizeBlock.Text = editTarget.Meta.RightTrackSize.ToString();

            foreach (var author in editTarget.Meta.LevelAuthors)
            {
                this.levelAuthors.Add(author);
            }

            foreach (var composer in editTarget.Meta.Composers)
            {
                this.composers.Add(composer);
            }

            this.bpmBox.Text = editTarget.Meta.Bpm.ToString();
            this.vectorValueXBox.Text = editTarget.Meta.NoteAppearPosition.X.ToString();
            this.vectorValueYBox.Text = editTarget.Meta.NoteAppearPosition.Y.ToString();
            this.vectorValueZBox.Text = editTarget.Meta.NoteAppearPosition.Z.ToString();

        }

        private async void BPMButtonDown(object sender, RoutedEventArgs e)
        {
            try
            {
                this.measureBPMButton.IsEnabled = false;
                this.measureBPMButton.Content = "测量中……";
                var bpm = await new BPMGetter(
                    new MemoryStream(this.editTarget.Meta.MusicFile), this.editTarget.Meta.Ext)
                    .Run();
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

        private void Apply(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            button.IsEnabled = false;

            try
            {
                button.Content = "正在构建项目……";

                this.editTarget.Meta.AssetBundleName = this.assetBundleBox.Text;
                this.editTarget.Meta.Name = this.musicTitleBox.Text;
                this.editTarget.Meta.Description = this.musicSubtitleBox.Text;
                this.editTarget.Meta.Difficulty = int.Parse(this.difficultyBlock.Text);
                this.editTarget.Meta.LeftTrackSize = int.Parse(this.leftTrackSizeBlock.Text);
                this.editTarget.Meta.RightTrackSize = int.Parse(this.rightTrackSizeBlock.Text);
                this.editTarget.Meta.LevelAuthors = this.levelAuthors.ToArray();
                this.editTarget.Meta.Composers = this.composers.ToArray();
                this.editTarget.Meta.Bpm = float.Parse(this.bpmBox.Text);
                this.editTarget.Meta.NoteAppearPosition
                    = new TFS.KBBeat.Unity.UnityVector3(
                        float.Parse(this.vectorValueXBox.Text),
                        float.Parse(this.vectorValueYBox.Text),
                        float.Parse(this.vectorValueZBox.Text)
                        );

                this.Close();

                Project.ObservableCurrentProject.BroadCast();
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
