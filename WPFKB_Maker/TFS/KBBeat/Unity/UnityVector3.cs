using Newtonsoft.Json;

namespace WPFKB_Maker.TFS.KBBeat.Unity
{
    public class UnityVector3
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }
        public UnityVector3(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }
}
