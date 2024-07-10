using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace WPFKB_Maker.TFS.Sound
{
    public static class KBMakerWaveStream
    {
        public static WaveStream GetWaveStream(string path)
        {
            switch (new FileInfo(path).Extension.ToLower())
            {
                case ".wav":
                    return new WaveFileReader(path);
                case ".mp3":
                    return new Mp3FileReader(path);
                default:
                    throw new NotSupportedException("Not supported audio extension");
            }
        }

        public static WaveStream GetWaveStream(string extension, Stream stream)
        {
            switch (extension)
            {
                case ".wav":
                    return new WaveFileReader(stream);
                case ".mp3":
                    return new Mp3FileReader(stream);
                default:
                    throw new NotSupportedException("Not supported audio extension");
            }
        }
    }
}
