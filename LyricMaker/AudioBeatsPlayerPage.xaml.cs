using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace LyricMaker
{
	/// <summary>
	/// 可用于自身或导航至 Frame 内部的空白页。
	/// </summary>

	public sealed partial class AudioBeatsPlayerPage : Page
	{
		private ObservableCollection<float> waveformFloatData { get; set; }
		private ObservableCollection<Lyric> lyricList { get; set; }

		private AudioGraphGlobal graphGlobal;

		private double prev_position;

		public AudioBeatsPlayerPage()
		{
			this.InitializeComponent();
			this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
			captionGridView.DataContext = this;
			waveformFloatData = new ObservableCollection<float>();
			lyricList = new ObservableCollection<Lyric>();
		}

		protected override async void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			backButton.IsEnabled = this.Frame.CanGoBack;
			switch (e.NavigationMode)
			{
				case NavigationMode.New:
					if (e.Parameter != null && !(e.Parameter is ""))
					{
						if (lyricList != null)
							lyricList.Clear();

						graphGlobal = (AudioGraphGlobal)e.Parameter;
						if (graphGlobal.Playlist.Count > 0)
						{
							if (graphGlobal.id != 0)
							{
								//await graphGlobal.InitilizeAudioGraph(graphGlobal.Playlist[graphGlobal.id - 1].storageFile);
								var storageFile = graphGlobal.fileInputNode.SourceFile;
								Task.Run(()=>LoadAndProcessLyricFile(
									storageFile.Path.Replace(storageFile.Name, "") + storageFile.DisplayName,
									new string[] { ".bcc", ".lrc" }));
								graphGlobal.ConfigureAudioFrame();
								//graphGlobal.fileInputNode.Seek(new TimeSpan(0, 0, 0, 0, (int)graphGlobal.position_ms));
								//graphGlobal.fileInputNode.FileCompleted += FileInputNode_FileCompleted;
								graphGlobal.audioGraph.QuantumStarted += AudioGraph_QuantumStarted;
								graphGlobal.audioGraph.Start();
								if (lyricList.Count == 0)
									lyricMessagePanel.Visibility = Visibility.Visible;
								else
									lyricMessagePanel.Visibility = Visibility.Collapsed;
							}
						}

					}
					break;
			}
		}

		//private void FileInputNode_FileCompleted(AudioFileInputNode sender, object args)
		//{
		//	graphGlobal.fileInputNode.Reset();
		//}

		private async void LoadAndProcessLyricFile(string path, string[] formats)
		{
			foreach (var _ in formats)
			{
				if (File.Exists(path + _))
				{
					var storageFile = await StorageFile.GetFileFromPathAsync(path + _);
					string text = await FileIO.ReadTextAsync(storageFile);
					switch (_)
					{
						case ".bcc":
							LyricParser.BCCFormatLyric(text, lyricList);
							break;
						case ".lrc":
							LyricParser.LRCFormatLyric(text, lyricList);
							break;
					}
					return;
				}
			}
		}

		private async void AudioGraph_QuantumStarted(AudioGraph sender, object args)
		{
			var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
			await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				double currren_progress = graphGlobal.fileInputNode.Position.TotalSeconds / graphGlobal.fileInputNode.Duration.TotalSeconds;
				//byte color_r = (byte)(currren_progress * 255);
				//byte color_g = (byte)(currren_progress * 144);
				//byte color_b = (byte)(currren_progress * 33);
				//lyricList[(int)(currren_progress * (lyricList.Count - 1))].color = new SolidColorBrush(Color.FromArgb(255, color_r, color_g, color_b));
				progressRate.Value = currren_progress * 100;
				lyricScrollViewer.ScrollToVerticalOffset(currren_progress * lyricScrollViewer.ScrollableHeight + graphGlobal.fileInputNode.Position.TotalMilliseconds);
			});
			
			try
			{
				AudioFrame frame = graphGlobal.audioFrame.GetFrame();
				ProcessFrameOutput(frame);
			}
			catch
			{

			}
		}

		unsafe private void ProcessFrameOutput(AudioFrame frame)
		{
			using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
			using (IMemoryBufferReference reference = buffer.CreateReference())
			{
				byte* dataInBytes;
				uint capacityInBytes;

				((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

				float* dataInFloat = (float*)dataInBytes;
				int dataInFloatLength = (int)buffer.Length / sizeof(float);
				for (int i = 0; i < dataInFloatLength; i++)
				{
					try
					{
						waveformFloatData.Add(dataInFloat[i] * 200.0f + 300);
					}
					catch
					{

					}
				}
			}
		}

		private void DrawWaveformCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedDrawEventArgs args)
		{
			float xAxis = 0.0f;
			for (int i = 0; i < waveformFloatData.Count; i++)
			{
				if (i == 0) continue;
				Vector2 point1 = new Vector2(xAxis, waveformFloatData[i - 1]);
				Vector2 point2 = new Vector2(xAxis, waveformFloatData[i]);
				args.DrawingSession.DrawLine(point1, point2, Colors.Red, 1f);
				xAxis += 0.3f;
			}
			waveformFloatData.Clear();
		}

		private void BackClick(object sender, RoutedEventArgs e)
		{
			if (this.Frame.CanGoBack)
			{
				this.Frame.GoBack();
			}
		}

		private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
		{
			var picker = new Windows.Storage.Pickers.FileOpenPicker
			{
				SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary
			};
			picker.FileTypeFilter.Add(".bcc");
			picker.FileTypeFilter.Add(".lrc");
			StorageFile file = await picker.PickSingleFileAsync();
			if (file != null)
			{
				string text = await FileIO.ReadTextAsync(file);
				switch (file.FileType)
				{
					case ".bcc":
						LyricParser.BCCFormatLyric(text, lyricList);
						break;
					case ".lrc":
						LyricParser.LRCFormatLyric(text, lyricList);
						break;
				}
				lyricMessagePanel.Visibility = Visibility.Collapsed;
			}
		}

		private void OpenCloseCaption_Click(object sender, RoutedEventArgs e)
		{
			
			if (openCloseCaptionButtonIcon.Symbol == Symbol.ClosedCaption)
			{
				openCloseCaptionButtonIcon.Symbol = Symbol.Caption;
				splitView.IsPaneOpen = false;
			}
			else
			{
				openCloseCaptionButtonIcon.Symbol = Symbol.ClosedCaption;
				splitView.IsPaneOpen = true;
			}
		}

		private void AppBarButton_Click(object sender, RoutedEventArgs e)
		{
			var getButtonElem = (AppBarButton)sender;
			string selectedItem = getButtonElem.Tag.ToString();
			switch (selectedItem)
			{
				case "Previous":
					break;
				case "Next":
					break;
			}
		}

		private void PlayButton_Click(object sender, RoutedEventArgs e)
		{
			if (playButtonIcon.Symbol == Symbol.Pause)
			{
				playButtonIcon.Symbol = Symbol.Play;
				graphGlobal.audioGraph.Stop();
			}
			else
			{
				playButtonIcon.Symbol = Symbol.Pause;
				graphGlobal.audioGraph.Start();
			}
		}

		private void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			this.drawWaveformCanvas.RemoveFromVisualTree();
			this.drawWaveformCanvas = null;
		}
	}
}
