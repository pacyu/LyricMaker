using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace LyricMaker
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed partial class MainPage : Page
    {
        private AudioGraphGlobal graphGlobal;
        private int prev_id;

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            graphGlobal = new AudioGraphGlobal();
            graphGlobal.Playlist = new ObservableCollection<Song>();
            prev_id = -1;
        }



        async protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            switch (e.NavigationMode)
            {
                case NavigationMode.Refresh:
                    break;
                case NavigationMode.Back:
                    forwardButton.IsEnabled = this.Frame.CanGoForward;
                    forwardButton.Visibility = Visibility.Visible;
                    break;
                case NavigationMode.New:
                    {
                        if (e.Parameter != null && !(e.Parameter is ""))
                        {
                            //var cacheSize = ((Frame)Parent).CacheSize;
                            //((Frame)Parent).CacheSize = 0;
                            //((Frame)Parent).CacheSize = cacheSize;
                            List<StorageFile> files = (List<StorageFile>)e.Parameter;

                            foreach (var file in files)
                            {
                                MusicProperties musicProperties = await file.Properties.GetMusicPropertiesAsync();
                                graphGlobal.Playlist.Add(new Song
                                {
                                    albumTitle = musicProperties.Album is "" ? "未知专辑" : musicProperties.Album,
                                    artistName = musicProperties.Artist is "" ? "未知歌手" : musicProperties.Artist,
                                    duration = musicProperties.Duration.ToString(@"mm\:ss"),
                                    songTitle = musicProperties.Title is "" ? file.DisplayName : musicProperties.Title,
                                    storageFile = file,
                                    trackNumber = graphGlobal.Playlist.Count + 1,
                                    year = musicProperties.Year,
                                    genre = string.Join(" / ", musicProperties.Genre),
                                    producers = string.Join(" / ", musicProperties.Producers)
                                });
                            }
                        }
                        else if (graphGlobal.Playlist.Count == 0)
                        {
                            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                            try
                            {
                                var settingsFile = await StorageFile.GetFileFromPathAsync(storageFolder.Path + "\\settings.json");
                                if (settingsFile != null)
                                {
                                    string readSettingText = await FileIO.ReadTextAsync(settingsFile);
                                    JsonObject keyValuePairs = JsonObject.Parse(readSettingText);
                                    QueryOptions queryOption = new QueryOptions(CommonFileQuery.OrderByTitle,
                                new string[] { ".mp3", ".mp4", ".wma", ".wav", ".ogg", ".flac", ".mpa", ".mid", ".cda", ".aif", ".m4a" })
                                    {
                                        FolderDepth = FolderDepth.Deep
                                    };
                                    foreach (var _ in keyValuePairs["defaultMusicLibrary"].GetArray())
                                    {
                                        var folder = await StorageFolder.GetFolderFromPathAsync(_.GetString());
                                        var files = await folder.CreateFileQueryWithOptions(queryOption).GetFilesAsync();

                                        foreach (var file in files)
                                        {
                                            MusicProperties musicProperties = await file.Properties.GetMusicPropertiesAsync();
                                            graphGlobal.Playlist.Add(new Song
                                            {
                                                albumTitle = musicProperties.Album is "" ? "未知专辑" : musicProperties.Album,
                                                artistName = musicProperties.Artist is "" ? "未知歌手" : musicProperties.Artist,
                                                duration = musicProperties.Duration.ToString(@"mm\:ss"),
                                                songTitle = musicProperties.Title is "" ? file.DisplayName : musicProperties.Title,
                                                storageFile = file,
                                                trackNumber = graphGlobal.Playlist.Count + 1,
                                                year = musicProperties.Year,
                                                genre = string.Join(" / ", musicProperties.Genre),
                                                producers = string.Join(" / ", musicProperties.Producers),
                                                composers = string.Join(" / ", musicProperties.Composers)
                                            });
                                        }
                                    }
                                }
                            }
                            catch
                            {

                            }
                        }
                    }
                    break;
            }
            
        }

        private void NavViewInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                if (!Type.Equals(this.Frame.CurrentSourcePageType, typeof(SettingsPage)))
                {
                    this.Frame.Navigate(typeof(SettingsPage), null, new SuppressNavigationTransitionInfo());
                }
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                switch (navItemTag)
                {
                    case "Home":
                        if (!Type.Equals(this.Frame.CurrentSourcePageType, typeof(MainPage)))
                        {
                            this.Frame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo());
                        }
                        break;
                    case "Maker":
                        graphGlobal.Clear();
                        this.Frame.Navigate(typeof(LyricMakerPage), graphGlobal, new DrillInNavigationTransitionInfo());
                        break;
                }
            }
        }

        //private void NavViewBackRequest(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        //{
        //    if (navView.IsPaneOpen &&
        //        (navView.DisplayMode == NavigationViewDisplayMode.Compact ||
        //         navView.DisplayMode == NavigationViewDisplayMode.Minimal))
        //        return ;
        //    if (this.Frame.CanGoBack)
        //        this.Frame.GoBack();
        //}

        private void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            if (graphGlobal is null || graphGlobal.audioGraph is null)
                return;
            if (playOrPause.Symbol == Symbol.Pause)
            {
                graphGlobal.audioGraph.Stop();
                playOrPause.Symbol = Symbol.Play;
                playOrPauseText.Text = "播放";
            }
            else if (playOrPause.Symbol == Symbol.Play)
            {
                if (prev_id != -1)
                {
                    graphGlobal.audioGraph.Start();
                    playOrPause.Symbol = Symbol.Pause;
                    playOrPauseText.Text = "暂停";
                }
            }
        }

        private async void FileInputNode_FileCompleted(AudioFileInputNode sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => {
                    playOrPause.Symbol = Symbol.Play;
                    prev_id = -1;
            });
            graphGlobal.Clear();
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    switch (loopStateToggle.Label)
                    {
                        case "单曲循环":
                            TogglePlayStatus(graphGlobal.id);
                            break;
                        case "列表循环":
                            if (graphGlobal.id < graphGlobal.Playlist.Count)
                                graphGlobal.id += 1;
                            else
                                graphGlobal.id = 1;
                            TogglePlayStatus(graphGlobal.id);
                            break;
                        case "列表随机":
                            Random random = new Random();
                            graphGlobal.id = random.Next(0, graphGlobal.Playlist.Count);
                            TogglePlayStatus(graphGlobal.id);
                            break;
                    }
                });
        }

        private async void AudioGraph_QuantumStarted(AudioGraph sender, object args)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => {
                    durationProgressBlock.Text = graphGlobal.fileInputNode.Position.ToString(@"mm\:ss");
                    durationBlock.Text = graphGlobal.fileInputNode.Duration.ToString(@"mm\:ss");
                    try
                    {
                        playProgress.Value = graphGlobal.fileInputNode.Position.TotalSeconds / graphGlobal.fileInputNode.Duration.TotalSeconds * 100;
                    }
                    catch
                    {

                    }
                });
        }

        private void VolumeButtonClick(object sender, RoutedEventArgs e)
        {
            if (toggleVolumeState.Symbol == Symbol.Volume)
            {
                toggleVolumeState.Symbol = Symbol.Mute;
                graphGlobal.fileInputNode.OutgoingGain = 0;
            }
            else if (toggleVolumeState.Symbol == Symbol.Mute)
            {
                toggleVolumeState.Symbol = Symbol.Volume;
                graphGlobal.fileInputNode.OutgoingGain = volumeAdjustment.Value / 100;
            }
        }

        private async void TogglePlayStatus(int id)
        {
            var play = graphGlobal.Playlist[id - 1];
            StorageItemThumbnail thumbnail = await play.storageFile.GetThumbnailAsync(ThumbnailMode.MusicView, 300);
            if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(thumbnail);
                var imageBrush = new ImageBrush
                {
                    ImageSource = bitmapImage
                };
                albumArtThumb.Fill = imageBrush;
            }
            if (playOrPause.Symbol == Symbol.Play)
            {
                if (id == prev_id)
                {
                    graphGlobal.audioGraph.Start();
                }
                else
                {
                    prev_id = id;
                    songName.Text = play.songTitle;
                    songArtistAndAlbumName.Text = play.albumTitle + " · " + play.artistName;
                    durationBlock.Text = play.duration;
                    await graphGlobal.InitilizeAudioGraph(play.storageFile);
                    graphGlobal.fileInputNode.FileCompleted += FileInputNode_FileCompleted;
                    graphGlobal.audioGraph.QuantumStarted += AudioGraph_QuantumStarted;
                    graphGlobal.audioGraph.Start();
                }
                playOrPause.Symbol = Symbol.Pause;
                playOrPauseText.Text = "暂停";
            }
            else if (playOrPause.Symbol == Symbol.Pause)
            {
                playOrPause.Symbol = Symbol.Play;
                playOrPauseText.Text = "播放";
                graphGlobal.audioGraph.Stop();
                if (id != prev_id)
                {
                    graphGlobal.Clear();
                    TogglePlayStatus(id);
                }
            }
        }

        private void DoubleTappedRowToPlay(object sender, DoubleTappedRoutedEventArgs e)
        {
            graphGlobal.id = ((Song)((DataGrid)sender).SelectedItem).trackNumber;

            TogglePlayStatus(graphGlobal.id);
        }

        private void CommandBarEditSelected(int id)
        {

        }

        private void CommandBarClick(object sender, RoutedEventArgs e)
        {
            AppBarButton appBarButton = (AppBarButton)sender;
            string _name = appBarButton.Label;
            graphGlobal.id = ((Song)appBarButton.DataContext).trackNumber;
            switch (_name)
            {
                case "播放":
                    TogglePlayStatus(graphGlobal.id);
                    break;
                case "复制":
                    //
                    break;
                case "编辑":
                    CommandBarEditSelected(graphGlobal.id);
                    break;
            }
        }

        private void ChangeMusicStateClick(object sender, RoutedEventArgs e)
        {
            if (graphGlobal.Playlist.Count == 0) return;
            var stateMode = ((AppBarButton)sender).Tag;
            switch (stateMode)
            {
                case "prevSong":
                    switch (loopStateToggle.Label)
                    {
                        case "列表随机":
                            Random random = new Random();
                            graphGlobal.id = random.Next(0, graphGlobal.Playlist.Count);
                            TogglePlayStatus(graphGlobal.id);
                            break;
                        default:
                            if (graphGlobal.id > 1)
                                graphGlobal.id -= 1;
                            else
                                graphGlobal.id = 1;
                            TogglePlayStatus(graphGlobal.id);
                            break;
                    }
                    break;
                
                case "nextSong":
                    switch (loopStateToggle.Label)
                    {
                        case "列表随机":
                            Random random = new Random();
                            graphGlobal.id = random.Next(graphGlobal.id, graphGlobal.Playlist.Count);
                            TogglePlayStatus(graphGlobal.id);
                            break;
                        default:
                            if (graphGlobal.id < graphGlobal.Playlist.Count)
                                graphGlobal.id += 1;
                            else
                                graphGlobal.id = 1;
                            TogglePlayStatus(graphGlobal.id);
                            break;
                    }
                    break;
            }
        }

        private void LoopModeClick(object sender, RoutedEventArgs e)
        {
            var label = ((AppBarButton)sender).Label;
            switch (label)
            {
                case "单曲循环":
                    ((AppBarButton)sender).Icon = new SymbolIcon(Symbol.RepeatAll);
                    ((AppBarButton)sender).Label = "列表循环";
                    break;
                case "列表循环":
                    ((AppBarButton)sender).Icon = new SymbolIcon(Symbol.Shuffle);
                    ((AppBarButton)sender).Label = "列表随机";
                    break;
                case "列表随机":
                    ((AppBarButton)sender).Icon = new SymbolIcon(Symbol.RepeatOne);
                    ((AppBarButton)sender).Label = "单曲循环";
                    break;
            }
        }

        private void SliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                graphGlobal.fileInputNode.OutgoingGain = ((Slider)sender).Value / 100;
            }
            catch
            {

            }
        }

        private void OpenCloseCaption_Click(object sender, RoutedEventArgs e)
        {
            if (splitView.IsPaneOpen)
            {
                splitView.IsPaneOpen = false;
            }
            else
            {
                splitView.IsPaneOpen = true;
            }
        }

        //private void PlayList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        //{
        //    id = ((Song)((DataGrid)sender).SelectedItem).trackNumber;
        //    var flyout = FlyoutBase.GetAttachedFlyout((FrameworkElement)sender);
        //    var options = new FlyoutShowOptions()
        //    {
        //        Position = e.GetPosition((FrameworkElement)sender),
        //        ShowMode = FlyoutShowMode.Transient
        //    };
        //    flyout?.ShowAt((FrameworkElement)sender, options);
        //}

        private void PlayProgress_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Pointer ptr = e.Pointer;
            try
            {
                if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
                {
                    Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(playProgress);
                    if (ptrPt.Properties.IsLeftButtonPressed)
                    {
                        double position = (((Slider)sender).Value / 100) * graphGlobal.fileInputNode.Duration.TotalSeconds;
                        graphGlobal.fileInputNode.Seek(new TimeSpan(0, 0, (int)position));
                    }
                }
            }
            catch
            {

            }
        }

        private async void MakeLyric_DragEnter(object sender, DragEventArgs e)
        {
            ContentDialog noWifiDialog = new ContentDialog
            {
                Title = "残念",
                Content = "该功能暂未完成！",
                CloseButtonText = "Ok"
            };
            ContentDialogResult result;
            
            switch (((Grid)sender).Tag)
            {
                case "makeLyric":
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        var items = await e.DataView.GetStorageItemsAsync();
                        if (items.Count > 0)
                        {
                            this.Frame.Navigate(typeof(LyricMakerPage), items, new EntranceNavigationTransitionInfo());
                        }
                    }
                    else
                    {
                        graphGlobal.Clear();
                        this.Frame.Navigate(typeof(LyricMakerPage), graphGlobal, new EntranceNavigationTransitionInfo());
                    }
                    break;
                case "generateTimeNode":
                    result = await noWifiDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        return;
                    }
                    break;
                case "lyricGenerator":
                    result = await noWifiDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        return;
                    }
                    break;
            }
        }

        private void AlbumartPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                graphGlobal.audioGraph.Stop();
                this.Frame.Navigate(typeof(AudioBeatsPlayerPage), graphGlobal, new DrillInNavigationTransitionInfo());
            }
            catch
            {
                this.Frame.Navigate(typeof(AudioBeatsPlayerPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private void MakeLyric_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "制作歌词";
            //e.DragUIOverride.SetContentFromBitmapImage(
            //    new BitmapImage(
            //        new Uri("ms-appx:///Assets/CustomImage.png", UriKind.RelativeOrAbsolute)));
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }

        private void PlayList_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            args.Data.Properties.Add("playlist", sender);
        }

        private void SearchBox_QueryChanged(SearchBox sender, SearchBoxQueryChangedEventArgs args)
        {
            var result = new ObservableCollection<Song>(
                from item in graphGlobal.Playlist
                where item.songTitle.ToUpperInvariant().Contains(args.QueryText)
                || item.artistName.ToUpperInvariant().Contains(args.QueryText)
                || item.songTitle.ToLowerInvariant().Contains(args.QueryText)
                || item.artistName.ToLowerInvariant().Contains(args.QueryText)
                select item);
            playList.ItemsSource = result;
            searchResultMsg.Text = "为您找到 " + result.Count.ToString() + " 条结果";
        }

        private void SearchBox_QuerySubmitted(SearchBox sender, SearchBoxQuerySubmittedEventArgs args)
        {
            var result = new ObservableCollection<Song>(
                from item in graphGlobal.Playlist
                where item.songTitle.ToUpperInvariant().Contains(args.QueryText)
                || item.artistName.ToUpperInvariant().Contains(args.QueryText)
                || item.songTitle.ToLowerInvariant().Contains(args.QueryText)
                || item.artistName.ToLowerInvariant().Contains(args.QueryText)
                select item);
            playList.ItemsSource = result;
            searchResultMsg.Text = "为您找到 " + result.Count.ToString() + " 条结果";
        }

        private void PlayList_Tapped(object sender, TappedRoutedEventArgs e)
        {
            graphGlobal.id = ((Song)((DataGrid)sender).SelectedItem).trackNumber;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoForward)
                Frame.GoForward();
        }
    }
}
