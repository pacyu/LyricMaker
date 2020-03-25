using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Storage;

namespace LyricMaker
{
	public class AudioGraphGlobal
	{
        public int id { get; set; }
        public double position_ms { get; set; }
        public ObservableCollection<Song> Playlist { get; set; }
        public AudioGraph audioGraph { get; set; }
        public AudioFileInputNode fileInputNode { get; set; }
        public AudioFrameOutputNode audioFrame { get; set; }
        public AudioDeviceOutputNode deviceOutputNode { get; set; }

        public AudioGraphGlobal()
        {

        }

        public async Task InitilizeAudioGraph(StorageFile file)
        {
            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                return;
            }

            audioGraph = result.Graph;
            if (audioGraph == null)
                return;

            CreateAudioFileInputNodeResult audioInputResult = await audioGraph.CreateFileInputNodeAsync(file);

            if (audioInputResult.Status != AudioFileNodeCreationStatus.Success)
            {
                return;
            }

            fileInputNode = audioInputResult.FileInputNode;

            CreateAudioDeviceOutputNodeResult audioOutputResult = await audioGraph.CreateDeviceOutputNodeAsync();

            if (audioOutputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                return;
            }

            deviceOutputNode = audioOutputResult.DeviceOutputNode;

            fileInputNode.AddOutgoingConnection(deviceOutputNode);
        }

        public void ConfigureAudioFrame()
        {
            audioFrame = audioGraph.CreateFrameOutputNode();
            fileInputNode.AddOutgoingConnection(audioFrame);
        }

        public void Clear()
        {
            try
            {
                audioFrame.Stop();
            }
            catch
            { 

            }

            try
            {
                deviceOutputNode.Stop();
            }
            catch
            {
                
            }

            try
            {
                position_ms = fileInputNode.Position.TotalMilliseconds;
                fileInputNode.Stop();
            }
            catch
            {
                
            }

            if (audioGraph != null)
            {
                audioGraph.Stop();
                audioGraph.ResetAllNodes();
                audioGraph.Dispose();
            }
            
            
        }
    }

}
