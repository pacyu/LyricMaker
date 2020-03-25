using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace LyricMaker
{
	public class Lyric
	{
		public string subtitle { get; set; }
		public TimeSpan startTime { get; set; }
		public TimeSpan endTime { get; set; }
		public SolidColorBrush color { get; set; }
		public int id { get; set; }
		public Lyric()
		{

		}
		public Lyric(string subtitle, int id, TimeSpan startTime, TimeSpan endTime)
		{
			this.subtitle = subtitle;
			this.id = id;
			this.startTime = startTime;
			this.endTime = endTime;
			this.color = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
		}
	}
}
