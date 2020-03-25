using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace LyricMaker
{
	/// <summary>
	/// 可用于自身或导航至 Frame 内部的空白页。
	/// </summary>
	public sealed partial class LyricMakerPage : Page
	{
		private ObservableCollection<Lyric> lyrics;
		private AudioGraphGlobal graphGlobal { get; set; }
		private double prev_slider_val;
		private int subtitle_counter;

		// 
		private double scanLinePixelRange;
		private double scanLinePixelRangeTotalMilliseconds;

		// 
		private double prev_point_x;
		private double prev_edit_point_x;
		//private PointerPoint _anchorPoint;
		//private PointerPoint _currentPoint;
		//private readonly TranslateTransform _transform = new TranslateTransform();
		private bool _isOnBorderRange;
		private bool _isScanSliderDrag;

		public static readonly DependencyProperty TextBoxProperty = DependencyProperty.Register("textBox", typeof(TextBox), typeof(LyricMakerPage), null);

		public LyricMakerPage()
		{
			this.InitializeComponent();
			this.NavigationCacheMode = NavigationCacheMode.Enabled;
			scanLinePixelRange = 624;
			scanLinePixelRangeTotalMilliseconds = (scanLinePixelRange / 9.0) * 200.0;
			lyrics = new ObservableCollection<Lyric>();
			controlScanLine.AddHandler(PointerPressedEvent, new PointerEventHandler(ControlScanLine_PointerPressed), true);
			controlScanLine.AddHandler(PointerReleasedEvent, new PointerEventHandler(ControlScanLine_PointerReleased), true);
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			backButton.IsEnabled = this.Frame.CanGoBack;
			if (e.NavigationMode == NavigationMode.New)
			{
				graphGlobal = (AudioGraphGlobal)e.Parameter;
				if (graphGlobal.Playlist.Count > 0)
				{
					if (graphGlobal.id != 0)
						Initilize_AudioGraph(graphGlobal.Playlist[graphGlobal.id - 1].storageFile);
				}

			}
		}

		private async void BackButton_Click()
		{
			ContentDialog noWifiDialog = new ContentDialog
			{
				Title = "警告",
				Content = "确定返回上一页吗？如果现在返回，当前页面工作将不会保存!",
				CloseButtonText = "确定",
				PrimaryButtonText = "取消"
			};

			ContentDialogResult result = await noWifiDialog.ShowAsync();
			if (result == ContentDialogResult.Primary)
				return;
			if (this.Frame.CanGoBack)
				this.Frame.GoBack();
		}

		private async void HomeButton_Click()
		{
			ContentDialog noWifiDialog = new ContentDialog
			{
				Title = "警告",
				Content = "确定跳转到主页吗？如果现在跳转，当前页面工作将不会保存!",
				CloseButtonText = "确定",
				PrimaryButtonText = "取消"
			};

			ContentDialogResult result = await noWifiDialog.ShowAsync();
			if (result == ContentDialogResult.Primary)
				return;
			this.Frame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
		}

		private async Task<bool> WarningDialog()
		{
			try
			{
				graphGlobal.Clear();
				if (subtitle_counter > 0)
				{
					ContentDialog noWifiDialog = new ContentDialog
					{
						Title = "警告",
						Content = "检测到您有未保存信息？如果现在切换，当前制作信息将不会保存!",
						CloseButtonText = "取消",
						PrimaryButtonText = "确定"
					};

					ContentDialogResult result = await noWifiDialog.ShowAsync();
					if (result == ContentDialogResult.Primary)
						return true;
					else
						return false;
				}
			}
			catch
			{
				return true;
			}
			return true;
		}

		private async void ListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
		{
			if (await WarningDialog())
			{
				ClearGlobalVariable();
				var item = ((ListView)sender).SelectedItem as Song;
				FillMusicPropertiesLayout(item.storageFile);
				await Initilize_AudioGraph(item.storageFile);
				GeneratorScale();
			}
		}

		private async void FillMusicPropertiesLayout(StorageFile storageFile)
		{
			var thumbnailitem = await storageFile.GetThumbnailAsync(ThumbnailMode.MusicView);
			if (thumbnailitem != null && thumbnailitem.Type == ThumbnailType.Image)
			{
				var bitmapImage = new BitmapImage();
				bitmapImage.SetSource(thumbnailitem);
				var imageBrush = new ImageBrush
				{
					ImageSource = bitmapImage
				};
				albumArtThumb.Fill = imageBrush;
			}
			MusicProperties musicProperties = await storageFile.Properties.GetMusicPropertiesAsync();
			titleTextBlock.Text = musicProperties.Title;
			artistTextBlock.Text = musicProperties.Artist;
			isCaptionExist.Visibility = Visibility.Visible;
		}

		private void ClearGlobalVariable()
		{
			progressBarControl.Value = 0;
			subtitle_counter = 0;
			lyrics.Clear();
			titleTextBlock.Text = "";
			artistTextBlock.Text = "";
			playIcon.Symbol = Symbol.Play;
			appendSubtitles.Children.Clear();
			subtitlesCanvas.Children.Clear();
			timeScalePanel.Children.Clear();
		}

		private void GeneratorScale()
		{
			for (double t = 0.0; t < graphGlobal.fileInputNode.Duration.TotalMilliseconds - 1000.0; t += 1000.0)
			{
				StackPanel stackPanel = new StackPanel
				{
					Name = "timeFrameStackPanel" + ((int)(t)).ToString(),
					Orientation = Orientation.Vertical,
					HorizontalAlignment = HorizontalAlignment.Left,
					Margin = new Thickness(0.0, 0.0, 11.0, 0.0)
				};

				int i = 0;
				Canvas canvas = new Canvas();
				for (double ms = 0.0; ms < 1000.0; ms += 200.0)
				{
					Line line = new Line
					{
						Name = "line" + ((int)t).ToString(),
						X1 = 9 * i,
						X2 = 9 * i,
						Y1 = 0
					};
					if ((int)ms == 0)
					{
						line.Y2 = 15.0;
						line.StrokeThickness = 1;
					}
					else
					{
						line.Y2 = 8.0;
						line.StrokeThickness = 1;
					}
					line.Stroke = new SolidColorBrush(Color.FromArgb(240, 114, 114, 114));
					i++;
					canvas.Children.Add(line);
				}
				stackPanel.Children.Add(canvas);

				TextBlock textBlock = new TextBlock
				{
					Name = "timeNode" + ((int)(t)).ToString(),
					Text = new TimeSpan(0, 0, 0, 0, (int)t).ToString(@"mm\:ss"),
					Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
					Foreground = new SolidColorBrush(Color.FromArgb(240, 114, 114, 114))
				};

				stackPanel.Children.Add(textBlock);
				timeScalePanel.Children.Add(stackPanel);
			}
		}

		private async Task Initilize_AudioGraph(StorageFile file)
		{
			await graphGlobal.InitilizeAudioGraph(file);
			graphGlobal.fileInputNode.FileCompleted += FileInputNode_FileCompleted;
			graphGlobal.audioGraph.QuantumStarted += AudioGraph_QuantumStarted;
		}

		private async void AudioGraph_QuantumStarted(AudioGraph sender, object args)
		{
			var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
			await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
				() =>
				{
					try
					{
						var rotate = new RotateTransform
						{
							Angle = ((RotateTransform)albumArtThumb.RenderTransform).Angle + 0.2
						};
						albumArtThumb.RenderTransform = rotate;
						timeProgress.Text = graphGlobal.fileInputNode.Position.Duration().ToString(@"mm\:ss");
						progressBarControl.Value = graphGlobal.fileInputNode.Position.TotalSeconds / graphGlobal.fileInputNode.Duration.TotalSeconds * 100;
						var remainder_ms = graphGlobal.fileInputNode.Duration.TotalMilliseconds - graphGlobal.fileInputNode.Position.TotalMilliseconds;
						if (remainder_ms <= scanLinePixelRangeTotalMilliseconds)
						{
							controlScanLine.Value = 100 * (1 - remainder_ms / scanLinePixelRangeTotalMilliseconds);
						}

					}
					catch
					{

					}
				});
		}

		private async void FileInputNode_FileCompleted(AudioFileInputNode sender, object args)
		{
			var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
			await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
				() =>
				{
					try
					{
						playIcon.Symbol = Symbol.Play;
						
					}
					catch
					{

					}
				});
			graphGlobal.audioGraph.Stop();
			graphGlobal.fileInputNode.Reset();
		}

		private void PlayButton_Click(object sender, RoutedEventArgs e)
		{
			switch (playIcon.Symbol)
			{
				case Symbol.Play:
					try
					{
						graphGlobal.audioGraph.Start();
						playIcon.Symbol = Symbol.Pause;
					}
					catch
					{

					}
					break;
				case Symbol.Pause:
					try
					{
						graphGlobal.audioGraph.Stop();
						playIcon.Symbol = Symbol.Play;
					}
					catch
					{

					}
					break;
			}
		}

		private void OpenPane_Click(object sender, RoutedEventArgs e)
		{
			if (splitViewPane.IsPaneOpen)
			{
				splitViewPane.IsPaneOpen = false;
				changeOpenPaneIcon.Symbol = Symbol.ClosePane;
			}
			else
			{
				splitViewPane.IsPaneOpen = true;
				changeOpenPaneIcon.Symbol = Symbol.OpenPane;
			}
		}

		private async void SaveLyric_Click()
		{
			var savePicker = new Windows.Storage.Pickers.FileSavePicker
			{
				SuggestedFileName = titleTextBlock.Text + ".lrc"
			};
			savePicker.FileTypeChoices.Add("Lyric Text", new List<string>() { ".bcc", ".lrc", ".txt" });
			StorageFile file = await savePicker.PickSaveFileAsync();
			if (file != null)
			{
				string captionText = "";
				switch (file.FileType)
				{
					case ".bcc":
						GeneratorBCCFormatLyric(captionText);
						break;
					case ".lrc":
						GeneratorLRCFormatLyric(captionText);
						break;
					case ".txt":
						GeneratorTXTFormatLyric(captionText);
						break;
				}
				await FileIO.WriteTextAsync(file, captionText);
				Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
				if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
				{

					this.Frame.Navigate(typeof(AudioBeatsPlayerPage));
				}
				else
				{
					ContentDialog noWifiDialog = new ContentDialog
					{
						Title = "未知错误",
						Content = "保存失败!",
						CloseButtonText = "了解",
					};

					ContentDialogResult result = await noWifiDialog.ShowAsync();
					if (result == ContentDialogResult.Primary)
						return;
				}
			}
			else
			{
				ContentDialog noWifiDialog = new ContentDialog
				{
					Title = "提示",
					Content = "已取消保存",
					CloseButtonText = "了解",
				};

				ContentDialogResult result = await noWifiDialog.ShowAsync();
				if (result == ContentDialogResult.Primary)
					return;
			}
		}

		private void GeneratorBCCFormatLyric(string captionText)
		{

		}

		private async void GeneratorLRCFormatLyric(string captionText)
		{
			MusicProperties musicProperties = await graphGlobal.fileInputNode.SourceFile.Properties.GetMusicPropertiesAsync();
			captionText += "[ar:" + musicProperties.Artist + "]\r\n";
			captionText += "[al:" + musicProperties.AlbumArtist + "]\r\n";
			captionText += "[ti:" + musicProperties.Title + "]\r\n";
			captionText += "[length:" + musicProperties.Duration.ToString(@"mm\:ss\.ff") + "]\r\n";
			captionText += "[by:" + "]\r\n";
			captionText += "[re:LyricMaker]\r\n";
			captionText += "[ve:" + "]\r\n";
			captionText += "\r\n";
			foreach (var _ in lyrics)
			{
				captionText += "[" + _.startTime + "]" + _.subtitle + "\r\n";
				captionText += "[" + _.endTime + "]" + "\r\n";
			}
		}

		private void GeneratorTXTFormatLyric(string captionText)
		{

		}

		[Obsolete]
		private void ProgressBarControl_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (!_isScanSliderDrag)
			{
				var _slider = sender as Slider;
				scrollViewCaption.ScrollToHorizontalOffset(
					scrollViewCaption.ScrollableWidth * (1 - (1 - _slider.Value / _slider.Maximum)) +
					(scrollViewCaption.ViewportWidth * _slider.Value / _slider.Maximum) -
					Math.Abs(controlScanLine.Value - prev_slider_val) * 0.45977233);
			}
		}

		private void ProgressBarControl_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			Pointer ptr = e.Pointer;
			try
			{
				if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
				{
					PointerPoint ptrPt = e.GetCurrentPoint(progressBarControl);
					if (ptrPt.Properties.IsLeftButtonPressed)
					{
						double _pos = ((Slider)sender).Value / 100 * graphGlobal.fileInputNode.Duration.TotalSeconds;
						graphGlobal.fileInputNode.Seek(new TimeSpan(0, 0, 0, (int)_pos));
					}
				}
			}
			catch
			{

			}
		}

		private void ControlScanLine_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			var _slider = (Slider)sender;
			scanLine.X1 = _slider.Value / 100 * scanLinePixelRange;
			scanLine.X2 = scanLine.X1;
		}

		private void ControlScanLine_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			var _slider = (Slider)sender;
			_isScanSliderDrag = true;
			prev_slider_val = _slider.Value;
		}

		private void ControlScanLine_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			
			Pointer ptr = e.Pointer;
			try
			{
				if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
				{
					PointerPoint ptrPt = e.GetCurrentPoint(controlScanLine);
					if (ptrPt.Properties.IsLeftButtonPressed)
					{
						graphGlobal.audioGraph.Stop();
						playIcon.Symbol = Symbol.Play;
					}

				}
			}
			catch
			{

			}
		}

		private void ControlScanLine_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			var _slider = (Slider)sender;
			try
			{
				double _goto = graphGlobal.fileInputNode.Position.TotalMilliseconds + scanLinePixelRangeTotalMilliseconds / 100 * (_slider.Value - prev_slider_val);
				graphGlobal.fileInputNode.Seek(new TimeSpan(0, 0, 0, 0, (int)_goto));

				graphGlobal.audioGraph.Start();
				playIcon.Symbol = Symbol.Pause;
				_isScanSliderDrag = false;
			}
			catch
			{

			}

		}

		// 计算出放置字幕的位置，设置宽度以表示持续时长
		private void AddCaption_Click(object sender, RoutedEventArgs e)
		{
			if (inputSubtitle.Text is "") return;
			try
			{
				_ = graphGlobal.fileInputNode.Position;
			}
			catch
			{
				return;
			}
			TextBox textBox1 = new TextBox
			{
				Name = "subtitleEdit" + subtitle_counter.ToString(),
				Text = inputSubtitle.Text,
				PlaceholderText = "请在此输入字幕",
				CornerRadius = new CornerRadius(4),
				BorderThickness = new Thickness(1, 1, 1, 1),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 151, 186, 214)),
				CanDrag = true,
				Width = 9 * 5 * 5,
				IsReadOnly = true,
				ManipulationMode = ManipulationModes.All
			};
			//textBox1.AddHandler(ManipulationStartingEvent, new ManipulationStartingEventHandler(TextBox1_ManipulationStarting), true);
			//textBox1.AddHandler(ManipulationStartedEvent, new ManipulationStartedEventHandler(TextBox1_ManipulationStarted), true);
			//textBox1.AddHandler(ManipulationDeltaEvent, new ManipulationDeltaEventHandler(TextBox1_ManipulationDelta), true);
			//textBox1.AddHandler(ManipulationCompletedEvent, new ManipulationCompletedEventHandler(TextBox1_ManipulationCompleted), true);
			//textBox1.ManipulationStarting += TextBox1_ManipulationStarting;
			//textBox1.ManipulationStarted += TextBox1_ManipulationStarted;
			//textBox1.ManipulationDelta += TextBox1_ManipulationDelta;
			//textBox1.ManipulationCompleted += TextBox1_ManipulationCompleted;
			textBox1.AddHandler(PointerPressedEvent, new PointerEventHandler(TextBox1_PointerPressed), true);
			textBox1.AddHandler(PointerMovedEvent, new PointerEventHandler(TextBox1_PointerMoved), true);
			textBox1.AddHandler(PointerReleasedEvent, new PointerEventHandler(TextBox1_PointerReleased), true);
			textBox1.PointerEntered += TextBox1_PointerEntered;
			textBox1.PointerExited += TextBox1_PointerExited;
			textBox1.PointerPressed += TextBox1_PointerPressed;
			textBox1.PointerMoved += TextBox1_PointerMoved;
			textBox1.PointerReleased += TextBox1_PointerReleased;
			textBox1.TextChanged += TextBox1_TextChanged;

			var textBoxPixelPosition = graphGlobal.fileInputNode.Position.TotalMilliseconds / 200.0 * 9;
			//Canvas.SetZIndex(textBox1, 0);
			Canvas.SetLeft(textBox1, textBoxPixelPosition);
			subtitlesCanvas.Children.Add(textBox1);

			TextBox textBox2 = new TextBox
			{
				Name = "subtitleText" + subtitle_counter.ToString(),
				Text = inputSubtitle.Text,
				PlaceholderText = "请在此输入字幕",
				CornerRadius = new CornerRadius(2),
				BorderThickness = new Thickness(1, 1, 1, 1),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 151, 186, 214)),
				Margin = new Thickness(5, 5, 0, 0),
			};
			var accelerator = new KeyboardAccelerator()
			{
				Modifiers = Windows.System.VirtualKeyModifiers.Shift,
				Key = Windows.System.VirtualKey.Enter,
			};
			accelerator.SetValue(TextBoxProperty, textBox2);
			accelerator.Invoked += Accelerator_Invoked;
			textBox2.KeyboardAccelerators.Add(accelerator);

			Button deleteButton = new Button
			{
				Name = "deleteButton" + subtitle_counter.ToString(),
				Content = new SymbolIcon(Symbol.Delete),
				BorderThickness = new Thickness(0),
				Margin = new Thickness(5, 5, 0, 0),
				HorizontalAlignment = HorizontalAlignment.Right
			};
			deleteButton.Click += DeleteButton_Click;

			StackPanel subtitleCrudPanel = new StackPanel
			{
				Name = "sutitleCrudPanel" + subtitle_counter.ToString(),
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Right,
				Width = 300,
			};
			subtitleCrudPanel.Children.Add(textBox2);
			subtitleCrudPanel.Children.Add(deleteButton);

			var subtitleHeadTimeNode = new TimeSpan(0, 0, 0, 0, (int)graphGlobal.fileInputNode.Position.TotalMilliseconds);
			TextBox textStartTime = new TextBox
			{
				Name = "textStartTime" + subtitle_counter.ToString(),
				Text = subtitleHeadTimeNode.ToString(@"mm\:ss\.ff"),
				CornerRadius = new CornerRadius(2),
				BorderThickness = new Thickness(1, 1, 1, 1),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 151, 186, 214)),
				VerticalContentAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Margin = new Thickness(0, 5, 0, 0)
			};

			var subtitleRearTimeNode = new TimeSpan(0, 0, 0, 0, (int)(subtitleHeadTimeNode.TotalMilliseconds + (textBox1.Width / 9) * 200));
			TextBox textEndTime = new TextBox
			{
				Name = "textEndTime" + subtitle_counter.ToString(),
				Text = subtitleRearTimeNode.ToString(@"mm\:ss\.ff"),
				CornerRadius = new CornerRadius(2),
				BorderThickness = new Thickness(1, 1, 1, 1),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 151, 186, 214)),
				VerticalContentAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Margin = new Thickness(0, 5, 0, 0)
			};

			StackPanel timeNodeStackPanel = new StackPanel
			{
				Name = "timeNodeStackPanel" + subtitle_counter.ToString(),
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0, 0, 0, 5)
			};
			timeNodeStackPanel.Children.Add(textStartTime);
			timeNodeStackPanel.Children.Add(textEndTime);

			StackPanel stackPanel1 = new StackPanel
			{
				Name = "rowSubtitlePanel" + subtitle_counter.ToString(),
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Left,
				Width = 400,
			};
			stackPanel1.Children.Add(timeNodeStackPanel);
			stackPanel1.Children.Add(subtitleCrudPanel);

			appendSubtitles.Children.Add(stackPanel1);
			
			lyrics.Add(new Lyric(inputSubtitle.Text, subtitle_counter, subtitleHeadTimeNode, subtitleRearTimeNode));
			subtitle_counter++;
			if (isCaptionExist.Visibility != Visibility.Collapsed)
				isCaptionExist.Visibility = Visibility.Collapsed;
			inputSubtitle.Text = "";
			scrollViewSubtitles.ScrollToVerticalOffset(scrollViewSubtitles.ScrollableHeight);
		}

		private void Accelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
		{
			TextBox editCaptionBox = sender.GetValue(TextBoxProperty) as TextBox;
			string _number = editCaptionBox.Name.Replace("subtitleText", "");
			TextBox editSubtitleBox = (TextBox)appendSubtitles.FindName("subtitleEdit" + _number);
			editSubtitleBox.Text = editCaptionBox.Text;
			foreach (var _ in lyrics)
			{
				if (_.id == int.Parse(_number))
				{
					_.subtitle = editCaptionBox.Text;
					break;
				}
			}
		}

		private void TextBox1_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox editSubtitleBox = sender as TextBox;
			string _number = editSubtitleBox.Name.Replace("subtitleEdit", "");
			TextBox editCaptionBox = (TextBox)appendSubtitles.FindName("subtitleText" + _number);
			editCaptionBox.Text = editSubtitleBox.Text;
			foreach (var _ in lyrics)
			{
				if (_.id == int.Parse(_number))
				{
					_.subtitle = editCaptionBox.Text;
					break;
				}
			}
		}

		//private void TextBox1_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
		//{
		//	//throw new NotImplementedException();
		//}

		//private void TextBox1_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
		//{
		//	TextBox textBox = sender as TextBox;

		//}

		//private void TextBox1_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
		//{
		//	TextBox editBox = sender as TextBox;
		//	Canvas.SetLeft(editBox, Canvas.GetLeft(editBox) + e.Delta.Translation.X);
		//}

		//private void TextBox1_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
		//{
		//	//throw new NotImplementedException();
		//}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			Button getDelButton = (Button)sender;
			string _number = getDelButton.Name.Replace("deleteButton", "");
			foreach(var _ in lyrics)
			{
				if (_.id == int.Parse(_number))
				{
					lyrics.Remove(_);
					break;
				}
			}
			
			subtitlesCanvas.Children.Remove((TextBox)subtitlesCanvas.FindName("subtitleEdit" + _number));
			appendSubtitles.Children.Remove((StackPanel)appendSubtitles.FindName("rowSubtitlePanel" + _number));
			subtitle_counter--;
			if (subtitle_counter == 0)
				isCaptionExist.Visibility = Visibility.Visible;
		}

		private void TextBox1_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			TextBox textBox = (TextBox)sender;
			Canvas canvas = (Canvas)textBox.Parent;
			Grid grid = (Grid)canvas.Parent;
			ScrollViewer scrollViewer = (ScrollViewer)grid.Parent;
			PointerPoint _anchorPoint = e.GetCurrentPoint(scrollViewer);
			PointerPoint pp = e.GetCurrentPoint(textBox);
			double boxWidth = textBox.Width;
			if (pp.Position.X >= boxWidth - 1.01)
			{
				prev_edit_point_x = pp.Position.X;
				_isOnBorderRange = true;
			}
			else if (pp.Position.X < boxWidth - 1.01)
			{
				textBox.IsReadOnly = false;
				prev_point_x = _anchorPoint.Position.X;
				_isOnBorderRange = false;
			}
			if (textBox != null)
				textBox.CapturePointer(e.Pointer);
			e.Handled = true;
		}

		private void TextBox1_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
			{
				TextBox editTextBox = (TextBox)sender;
				Canvas canvas = (Canvas)editTextBox.Parent;
				Grid grid = (Grid)canvas.Parent;
				ScrollViewer scrollViewer = (ScrollViewer)grid.Parent;
				PointerPoint editPtr = e.GetCurrentPoint(editTextBox);
				if (editPtr.Properties.IsLeftButtonPressed)
				{
					if (_isOnBorderRange)
					{
						double offset_pixel = editPtr.Position.X - prev_edit_point_x;
						if (editTextBox.Width < 10)
						{
							if (offset_pixel > 0)
							{
								editTextBox.Width += offset_pixel;
							}
						}
						else
						{
							editTextBox.Width += offset_pixel;
						}
						prev_edit_point_x = editPtr.Position.X;
					}
					else
					{
						PointerPoint _currentPoint = e.GetCurrentPoint(scrollViewer);
						double offset_pixel = _currentPoint.Position.X - prev_point_x;
						if (Canvas.GetLeft(editTextBox) == 0)
						{
							if (offset_pixel > 0)
							{
								Canvas.SetLeft(editTextBox, Canvas.GetLeft(editTextBox) + offset_pixel);
							}
						}
						else if (Canvas.GetLeft(editTextBox) == scrollViewer.ExtentWidth)
						{
							if (offset_pixel < 0)
							{
								Canvas.SetLeft(editTextBox, Canvas.GetLeft(editTextBox) + offset_pixel);
							}
						}
						else
						{
							Canvas.SetLeft(editTextBox, Canvas.GetLeft(editTextBox) + offset_pixel);
						}
						prev_point_x = _currentPoint.Position.X;
					}
				}
			}
		}

		private void TextBox1_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			TextBox textBox = (TextBox)sender;

			string _number = textBox.Name.Replace("subtitleEdit", "");
			TextBox startTimeBox = (TextBox)appendSubtitles.FindName("textStartTime" + _number);
			double offsetPixelConvertToMilliseconds = Canvas.GetLeft(textBox);
			TimeSpan newStartTimeNode = new TimeSpan(0, 0, 0, 0, (int)(offsetPixelConvertToMilliseconds / 9.0 * 200.0));
			startTimeBox.Text = newStartTimeNode.ToString(@"mm\:ss\.ff");

			TextBox endTimeBox = (TextBox)appendSubtitles.FindName("textEndTime" + _number);
			double editBoxWidthConvertToMilliseconds = textBox.Width / 9.0 * 200.0;
			TimeSpan newEndTimeNode = new TimeSpan(0, 0, 0, 0, (int)(newStartTimeNode.TotalMilliseconds + editBoxWidthConvertToMilliseconds));
			endTimeBox.Text = newEndTimeNode.ToString(@"mm\:ss\.ff");

			foreach (var _ in lyrics)
			{
				if (_.id == int.Parse(_number))
				{
					_.startTime = newStartTimeNode;
					_.endTime = newEndTimeNode;
					break;
				}
			}
			if (textBox != null)
				textBox.ReleasePointerCapture(e.Pointer);
			e.Handled = true;
		}

		private void TextBox1_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			TextBox textBox = (TextBox)sender;
			PointerPoint pp = e.GetCurrentPoint(textBox);
			double boxWidth = textBox.Width;
			if (pp.Position.X >= boxWidth - 1.01)
			{
				Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.SizeWestEast, 1);
			}
			else
			{
				Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 2);
			}
			try
			{
				if (playIcon.Symbol != Symbol.Play)
				{
					playIcon.Symbol = Symbol.Play;
					graphGlobal.audioGraph.Stop();
				}
			}
			catch
			{

			}
		}

		private void TextBox1_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			((TextBox)sender).IsReadOnly = true;
			Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
		}

		//private void ScrollViewCaption_Drop(object sender, DragEventArgs e)
		//{
		//	var textBox1 = e.DataView.Properties["textBox1"] as TextBox;
		//	var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
		//	Canvas.SetLeft(textBox1, pointerPosition.X - Window.Current.Bounds.X);
		//}

		private void AppBarButton_Click(object sender, RoutedEventArgs e)
		{
			string selectedButtonTag = ((AppBarButton)sender).Tag.ToString();
			switch (selectedButtonTag)
			{
				case "Home":
					HomeButton_Click();
					break;
				case "Back":
					BackButton_Click();
					break;
				case "Save":
					SaveLyric_Click();
					break;
				
			}
		}

		private async void Grid_DragEnter(object sender, DragEventArgs e)
		{
			if (await WarningDialog())
			{
				IReadOnlyList<StorageFile> storageFiles = (IReadOnlyList<StorageFile>)await e.DataView.GetStorageItemsAsync();
				foreach (var _ in storageFiles)
				{
					var lrc_path = _.Path.Replace(_.Name, "") + _.DisplayName + ".lrc";
					StorageFile lrcFile = await StorageFile.GetFileFromPathAsync(lrc_path);
					string lrcText = await FileIO.ReadTextAsync(lrcFile);
					ClearGlobalVariable();
					FillMusicPropertiesLayout(_);
					Initilize_AudioGraph(_);
					//LyricParser.LRCFormatLyric(lrcText, lyrics);
					GeneratorScale();
				}
			}
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
			playlist.ItemsSource = result;
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
			playlist.ItemsSource = result;
		}
	}
}
