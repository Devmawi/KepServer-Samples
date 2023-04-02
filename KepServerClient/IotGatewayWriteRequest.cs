using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KepServerClient
{
    public class IotGatewayWriteRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("v")]
        public int Value { get; set; }
    }
}
