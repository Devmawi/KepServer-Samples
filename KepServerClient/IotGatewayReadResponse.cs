using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KepServerClient
{
    public class IotGatewayReadResponse
    {
        public class ReadResult
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
            [JsonPropertyName("v")]
            public int Value { get; set; }
            [JsonPropertyName("t")]
            public long Ticks { get; set; }
            public DateTime DateTime => DateTimeOffset.FromUnixTimeMilliseconds(Ticks).LocalDateTime;

           
        }

        [JsonPropertyName("readResults")]
        public ReadResult[] ReadResults { get; set; } = Array.Empty<ReadResult>();
    }
}
