using NAudio.Vorbis;
using NReco.VideoConverter;
using System.IO;
using System.Threading.Tasks;

namespace WPFKB_Maker.TFS.Sound
{
    public static class OggTransformer
    {
        private static FFMpegConverter converter = new FFMpegConverter();

        public static async Task TransformToOGGAsync(string inputFile, string savePath)
        {
            await Task.Run(() =>
            {
                converter.ConvertMedia(inputFile, savePath, Format.ogg);
            });
        }
    }
}
