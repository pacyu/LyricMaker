using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace LyricMaker
{
	/// <summary>
	/// 可用于自身或导航至 Frame 内部的空白页。
	/// </summary>
	public sealed partial class SettingsPage : Page
	{
		public SettingsPage()
		{
			this.InitializeComponent();
			this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
		}

		protected override async void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			navView.IsBackEnabled = this.Frame.CanGoBack;
			switch (e.NavigationMode)
			{
				case NavigationMode.New:
					if (musicPath.Text == "")
					{
						StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
						try
						{
							var settingsFile = await StorageFile.GetFileFromPathAsync(storageFolder.Path + "\\settings.json");
							if (settingsFile != null)
							{
								string readSettingText = await FileIO.ReadTextAsync(settingsFile);
								JsonObject keyValuePairs = JsonObject.Parse(readSettingText);
								var folders = keyValuePairs["defaultMusicLibrary"].GetArray();
								foreach (var _ in folders)
								{
									musicPath.Text += _.GetString();
									if (_ != folders[folders.Count - 1])
										musicPath.Text += ";";
								}
							}
						}
						catch
						{

						}
					}
					break;
			}
		}

		private void NavViewBackRequest(NavigationView sender, NavigationViewBackRequestedEventArgs args)
		{
			if (navView.IsPaneOpen &&
				(navView.DisplayMode == NavigationViewDisplayMode.Compact ||
				 navView.DisplayMode == NavigationViewDisplayMode.Minimal))
				return;
			if (this.Frame.CanGoBack)
				this.Frame.GoBack();
		}

		async private void OpenFolderClick(object sender, RoutedEventArgs e)
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker
			{
				SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary
			};
			folderPicker.FileTypeFilter.Add("*");
			StorageFolder folder = await folderPicker.PickSingleFolderAsync();
			if (folder != null)
			{
				Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
				musicPath.Text += folder.Path + ";";
			}
		}

		async private void SaveSettingClick(object sender, RoutedEventArgs e)
		{
			loadingControl.IsActive = true;
			loadingControl.Visibility = Visibility.Visible;
			if (musicPath.Text != "")
			{
				try
				{
					StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
					Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("SettingsFolder", storageFolder);
					try
					{
						var saveFile = await StorageFile.GetFileFromPathAsync(storageFolder.Path + "\\settings.json");
						string localSettingsText = await FileIO.ReadTextAsync(saveFile);
						string[] vs = musicPath.Text.Split(";");
						string settingsText = "{\"defaultMusicLibrary\": [";
						foreach (var _ in vs)
						{
							settingsText += "\"" + _.Replace("\\", "\\\\") + "\"";
							if (_ != vs[vs.Length - 1])
								settingsText += ", ";
						}
						settingsText += "]}";
						if (VerifyMd5Hash(settingsText, localSettingsText))
						{
							loadingControl.IsActive = false;
							loadingControl.Visibility = Visibility.Collapsed;
							messageFlyout.Text = "无更改...";
							return;
						}
						else
						{
							await FileIO.WriteTextAsync(saveFile, settingsText);
						}
					}
					catch
					{
						StorageFile createFile = await storageFolder.CreateFileAsync("settings.json", CreationCollisionOption.FailIfExists);
						string[] vs = musicPath.Text.Split(";");
						string settingsText = "{\"defaultMusicLibrary\": \"[";
						foreach (var _ in vs)
						{
							settingsText += "\"" + _.Replace("\\", "\\\\") + "\"";
							if (_ != vs[vs.Length - 1])
								settingsText += ", ";
						}
						settingsText += "]\"}";
						await FileIO.WriteTextAsync(createFile, settingsText);
					}
					QueryOptions queryOption = new QueryOptions(CommonFileQuery.OrderByTitle,
								new string[] { ".mp3", ".mp4", ".wma", ".wav", ".ogg", ".flac", ".mpa", ".mid", ".cda", ".aif", ".m4a" })
					{
						FolderDepth = FolderDepth.Deep
					};
					string[] folders = musicPath.Text.Split(';');
					List<StorageFile> files = new List<StorageFile>();
					foreach (var _ in folders)
					{
						var folder = await StorageFolder.GetFolderFromPathAsync(_);
						var _files = await folder.CreateFileQueryWithOptions(queryOption).GetFilesAsync();
						foreach (var file in _files)
						{
							files.Add(file);
						}
					}
					loadingControl.IsActive = false;
					loadingControl.Visibility = Visibility.Collapsed;
					messageFlyout.Text = "已保存！";
					this.Frame.Navigate(typeof(MainPage), files, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
				}
				catch
				{
					loadingControl.IsActive = false;
					loadingControl.Visibility = Visibility.Collapsed;
					messageFlyout.Text = "出现意外错误！";
				}
			}
			else
			{
				loadingControl.IsActive = false;
				loadingControl.Visibility = Visibility.Collapsed;
				messageFlyout.Text = "请添加至少一个目录！";
			}
		}

		private string GetMd5Hash(string input)
		{
			MD5 md5Hash = MD5.Create();
			byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

			StringBuilder sBuilder = new StringBuilder();

			for (int i = 0; i < data.Length; i++)
			{
				sBuilder.Append(data[i].ToString("x2"));
			}
			return sBuilder.ToString();
		}

		private bool VerifyMd5Hash(string input, string source)
		{
			string hashOfInput = GetMd5Hash(input);
			string hashOfSource = GetMd5Hash(source);
			StringComparer comparer = StringComparer.OrdinalIgnoreCase;

			if (0 == comparer.Compare(hashOfInput, hashOfSource))
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
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
				}
			}
		}
	}
}
