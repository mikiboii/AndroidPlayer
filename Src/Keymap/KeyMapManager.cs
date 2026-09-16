

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Androidplayer.Src.Keymap
{
    public class KeymapElement
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("keys")]
        public List<string> Keys { get; set; } = new();

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
        
        
        // ✅ New properties
        [JsonPropertyName("scaled_width")]
        public double ScaledWidth { get; set; }

        [JsonPropertyName("scaled_height")]
        public double ScaledHeight { get; set; }

        
        
        

        [JsonPropertyName("parent_width")]
        public double ParentWidth { get; set; }

        [JsonPropertyName("parent_height")]
        public double ParentHeight { get; set; }

        [JsonPropertyName("Img path")]
        public string? ImagePath { get; set; }


        [JsonPropertyName("app_name")]
        public string? AppName { get; set; }     // ✅ Added

        [JsonIgnore]
        public bool IsMultiKey => Keys != null && Keys.Count > 1;
    }

    public class KeyMapManager
    {
        public static KeyMapManager? Instance { get; private set; }

        private List<KeymapElement> _elements = new();

        private KeyMapManager() { }

        public static void LoadKeymap(string jsonData)
        {
            Instance ??= new KeyMapManager();
            Instance.ReloadData(jsonData);
        }

        private void ReloadData(string jsonData)
        {
            _elements.Clear();

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // ✅ Handles both { "miki": [ ... ] } and [ ... ]
                if (jsonData.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(jsonData);
                    var root = doc.RootElement;

                    foreach (var property in root.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            var parsedList = JsonSerializer.Deserialize<List<KeymapElement>>(property.Value.GetRawText(), options);
                            if (parsedList != null)
                                _elements.AddRange(parsedList);
                        }
                    }
                }
                else
                {
                    var parsedList = JsonSerializer.Deserialize<List<KeymapElement>>(jsonData, options);
                    if (parsedList != null)
                        _elements = parsedList;
                }

                Console.WriteLine($"✅ [KeyMapManager] Loaded {_elements.Count} elements.");
                OverlayManager.Instance?.rerender_overlay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [KeyMapManager] JSON parse error: {ex.Message}");
            }
        }

        public void Clear() => _elements.Clear();

        public KeymapElement? GetElement(string elementType)
        {
            return _elements.Find(e => e.Type.Equals(elementType, StringComparison.OrdinalIgnoreCase));
        }

        public List<KeymapElement> GetElementsByType(string elementType)
        {
            return _elements.FindAll(e => e.Type.Equals(elementType, StringComparison.OrdinalIgnoreCase));
        }

        public List<KeymapElement> GetElementsByKey(string elementKey)
        {
            // return _elements.FindAll(e => e.Type.Equals(elementType, StringComparison.OrdinalIgnoreCase));
            return _elements.FindAll(e => e.Keys.Contains(elementKey) && !e.IsMultiKey );
        }
        
        public List<KeymapElement> Getmultikey_ElementsByKey(string elementKey)
        {
            // return _elements.FindAll(e => e.Type.Equals(elementType, StringComparison.OrdinalIgnoreCase));
            return _elements.FindAll(e => e.Keys.Contains(elementKey) && e.IsMultiKey);
        } 
        
        public List<KeymapElement> GetMultiKeyElements()
        {
            return _elements.FindAll(e => e.IsMultiKey);
        }

        public List<KeymapElement> GetAllElements()
        {
            return new List<KeymapElement>(_elements); // return a copy
        }

        public void PrintAll()
        {
            Console.WriteLine("== Loaded Keymap Elements ==");
            foreach (var el in _elements)
            {
                string keys = el.Keys != null && el.Keys.Count > 0 ? string.Join("+", el.Keys) : "(none)";
                string img = string.IsNullOrEmpty(el.ImagePath) ? "(no image)" : el.ImagePath;
                Console.WriteLine($"  - {el.Type}: Keys={keys}, X={el.X}, Y={el.Y}, W={el.Width}, H={el.Height}, Img={img}");
            }
        }
    }
}
