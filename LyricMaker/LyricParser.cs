using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace LyricMaker
{
	public class LyricParser
	{
		static public void LRCFormatLyric(string text, ObservableCollection<Lyric> lyricList)
		{
			Regex regex2 = new Regex(@"\[(?<startTime>\d{2}:\d{2}\.\d{2,})|(?<tag>\w{2,}):(?<val>.*)\](?<string>.*)");
			MatchCollection matches = regex2.Matches(text);

			for (int i = 0; i < matches.Count - 1; i++)
			{
				if (matches[i].Groups["startTime"].Success)
				{
					string[] start_mm = matches[i].Groups["startTime"].Value.Split(':');
					string[] start_ss = start_mm[1].Split('.');

					string[] end_mm = matches[i + 1].Groups["startTime"].Value.Split(':');
					string[] end_ss = end_mm[1].Split('.');

					string subtitle = matches[i].Groups["string"].Value is "\r" ? matches[i].Groups["string"].Value : matches[i].Groups["string"].Value.Replace("\r", "");
					TimeSpan startTime = new TimeSpan(0, 0, int.Parse(start_mm[0]), int.Parse(start_ss[0]), int.Parse(start_ss[1]));
					TimeSpan endTime = new TimeSpan(0, 0, int.Parse(end_mm[0]), int.Parse(end_ss[0]), int.Parse(end_ss[1]));
					lyricList.Add(new Lyric(subtitle, i, startTime, endTime));
				}
				else if (matches[i].Groups["tag"].Success)
				{

				}
			}
		}

		static public void BCCFormatLyric(string text, ObservableCollection<Lyric> lyricList)
		{
			JsonObject json_lyric = JsonObject.Parse(text);
			var bcclyric = json_lyric["body"].GetArray();
			int i = 0;
			foreach (var _ in bcclyric)
			{
				var obj = _.GetObject();
				string content = obj["content"].GetString();
				TimeSpan startTime = new TimeSpan(0, 0, 0, (int)obj["from"].GetNumber());
				TimeSpan endTime = new TimeSpan(0, 0, 0, (int)obj["to"].GetNumber());
				lyricList.Add(new Lyric(content, i, startTime, endTime));
				i++;
			}
		}
	}
}
