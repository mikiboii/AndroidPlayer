using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Androidplayer.Src.Keymap
{
    [JsonSerializable(typeof(List<KeymapElement>))]
    [JsonSerializable(typeof(KeymapElement))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    
    internal partial class KeymapJsonContext : JsonSerializerContext
    {
    }
}