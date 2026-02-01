using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace YoutubeServies
{

    public class YtDlpResult
    {
        [JsonPropertyName("title")]
        public string title { get; set; }

        [JsonPropertyName("uploader")]
        public string uploader { get; set; }

        [JsonPropertyName("formats")]
        public List<Format> formats { get; set; }
    }
}
