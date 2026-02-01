using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace YoutubeServies
{
    public class Format
    {
        [JsonPropertyName("format_id")]
        public string format_id { get; set; }

        [JsonPropertyName("ext")]
        public string ext { get; set; }

        [JsonPropertyName("height")]
        public int? height { get; set; }

        [JsonPropertyName("width")]
        public int? width { get; set; }

        [JsonPropertyName("filesize")]
        public long? filesize { get; set; }

        [JsonPropertyName("filesize_approx")]
        public long? filesize_approx { get; set; }

        [JsonPropertyName("vcodec")]
        public string vcodec { get; set; }

        [JsonPropertyName("acodec")]
        public string acodec { get; set; }

        [JsonPropertyName("fps")]
        public double? fps { get; set; }

        [JsonPropertyName("tbr")]
        public double? tbr { get; set; }

        public long? GetSize()
        {
            return filesize ?? filesize_approx;
        }
    }
}