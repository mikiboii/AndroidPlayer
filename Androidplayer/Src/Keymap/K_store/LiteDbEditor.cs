using System;
using System.Linq;
using LiteDB;

namespace Androidplayer.Src.Keymap.K_store
{
    public class LiteDbEditor
    {
        private readonly string _filePath;
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<BsonDocument> _col;
        private readonly LiteDbEditorOptions _options;

        public LiteDbEditor(string filePath, LiteDbEditorOptions options = null)
        {
            _filePath = filePath;
            _options = options ?? new LiteDbEditorOptions();
            _db = new LiteDatabase(_filePath);
            _col = _db.GetCollection<BsonDocument>("keymaps");

            // Ensure at least one document exists
            if (_col.Count() == 0)
            {
                _col.Insert(new BsonDocument());
            }
        }

        // Get value from dot-notated path
        public BsonValue Get(string path = null)
        {
            var doc = _col.FindAll().FirstOrDefault();
            if (doc == null) return BsonValue.Null;
            if (string.IsNullOrEmpty(path)) return doc;
            return TraversePath(doc, path.Split('.'));
        }

        // Set value at dot-notated path
        // public void Set(string path, BsonValue value)
        // {
        //     var doc = _col.FindAll().FirstOrDefault();
        //     if (doc == null) return;
        //
        //     SetPath(doc, path.Split('.'), value);
        //
        //     _col.Update(doc);
        //     if (_options.Autosave) Save();
        // }
        
        public void Set(string path, BsonValue value)
        {
            var doc = _col.FindAll().FirstOrDefault();
            if (doc == null) return;

            // ✅ If path is empty, replace the whole root document instead of nesting
            if (string.IsNullOrEmpty(path))
            {
                if (value.IsDocument)
                {
                    var newDoc = value.AsDocument;

                    // preserve existing _id if present
                    if (doc.ContainsKey("_id"))
                        newDoc["_id"] = doc["_id"];

                    _col.Update(newDoc);
                    if (_options.Autosave) Save();
                    return;
                }
                else
                {
                    throw new InvalidOperationException("Root value must be a BsonDocument.");
                }
            }

            // Normal nested path handling
            SetPath(doc, path.Split('.'), value);
            _col.Update(doc);
            if (_options.Autosave) Save();
        }


        // Remove a key
        public void Unset(string path) => Set(path, BsonValue.Null);

        // Append to array at path
        public void Append(string path, BsonValue value)
        {
            var arr = Get(path);
            if (arr.IsNull || !arr.IsArray)
            {
                arr = new BsonArray();
            }

            arr.AsArray.Add(value);
            Set(path, arr);
        }

        // Pop last element from array at path
        public void Pop(string path)
        {
            var arr = Get(path);
            if (arr.IsArray && arr.AsArray.Count > 0)
            {
                arr.AsArray.RemoveAt(arr.AsArray.Count - 1);
                Set(path, arr);
            }
        }

        // Save database (manual checkpoint)
        public void Save() => _db.Checkpoint();

        // ---- Private helpers ----

        private BsonValue TraversePath(BsonValue current, string[] keys)
        {
            foreach (var key in keys)
            {
                if (current is BsonDocument doc && doc.ContainsKey(key))
                {
                    current = doc[key];
                }
                else if (current is BsonArray arr && int.TryParse(key, out int index) && index >= 0 && index < arr.Count)
                {
                    current = arr[index];
                }
                else
                {
                    return BsonValue.Null;
                }
            }
            return current;
        }

        private void SetPath(BsonValue current, string[] keys, BsonValue value)
        {
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var key = keys[i];

                if (current is BsonDocument doc)
                {
                    if (!doc.ContainsKey(key) || doc[key].IsNull)
                        doc[key] = new BsonDocument();
                    current = doc[key];
                }
                else if (current is BsonArray arr && int.TryParse(key, out int index))
                {
                    while (arr.Count <= index) arr.Add(new BsonDocument());
                    current = arr[index];
                }
                else
                {
                    throw new InvalidOperationException("Invalid path in SetPath");
                }
            }

            var lastKey = keys.Last();
            if (current is BsonDocument finalDoc)
            {
                finalDoc[lastKey] = value;
            }
            else if (current is BsonArray finalArr && int.TryParse(lastKey, out int idx))
            {
                while (finalArr.Count <= idx) finalArr.Add(BsonValue.Null);
                finalArr[idx] = value;
            }
            else
            {
                throw new InvalidOperationException("Invalid path in SetPath (final step)");
            }
        }
    }

    public class LiteDbEditorOptions
    {
        public bool Autosave { get; set; } = false;
    }
}
