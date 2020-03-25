using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace LyricMaker
{
    public class Song
    {
        public int trackNumber { get; set; }
        public string songTitle { get; set; }
        public string artistName { get; set; }
        public string albumTitle { get; set; }
        public string genre { get; set; }
        public string composers { get; set; }
        public string producers { get; set; }
        public uint year { get; set; }
        public string duration { get; set; }
        public StorageFile storageFile { get; set; }
        public Song()
        {

        }
    }
}
