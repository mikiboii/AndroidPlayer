using System;
using System.IO;
using System.Threading;
using System.Windows;
using LiteDB;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap
{
    public class keymap_worker
    {
        private readonly string _filePath;
        private Thread? _workerThread;
        private bool _stopRequested = false;

        // ✅ Singleton instance
        public static keymap_worker? Instance { get; private set; }

        private keymap_worker()
        {
            _filePath = Path.Combine(Environment.CurrentDirectory, "user", "data.db");
        }

        // ✅ Initialize once or reuse
        public static keymap_worker GetInstance()
        {
            return Instance ??= new keymap_worker();
        }

        // ✅ Start / restart safely
        public void StartWorker()
        {
            StopWorker(); // prevent overlap

            _stopRequested = false;
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Keymap Worker Thread"
            };
            _workerThread.Start();
        }

        // ✅ Thread main logic
        private void WorkerLoop()
        {
            try
            {
                var editor = my_info.Instance.Dataeditor;
                if (editor == null)
                {
                    Console.WriteLine("❌ [keymap_worker] Dataeditor is null.");
                    return;
                }

                // Load DB root
                var allDocs = editor.Get();
                if (allDocs.IsNull)
                {
                    Console.WriteLine("⚙️ [keymap_worker] Initializing DB structure...");

                    var skeleton = new BsonDocument
                    {
                        ["keymaps"] = new BsonArray(),
                        ["default_keymap"] = ""
                    };
                    editor.Set("", skeleton);
                    return;
                }

                var root = allDocs.AsDocument;

                // Ensure required keys exist
                if (!root.ContainsKey("keymaps") || !root["keymaps"].IsArray)
                    root["keymaps"] = new BsonArray();

                if (!root.ContainsKey("default_keymap") || !root["default_keymap"].IsString)
                    root["default_keymap"] = "";

                editor.Set("", root);

                // ✅ Step 1: Get default keymap
                string defaultKeymap = editor.Get("default_keymap")?.AsString ?? "";
                if (string.IsNullOrEmpty(defaultKeymap))
                {
                    Console.WriteLine("⚠️ [keymap_worker] No default keymap set.");
                    return;
                }

                // ✅ Step 2: Find and load it
                var keymapsArray = editor.Get("keymaps");
                if (!keymapsArray.IsArray)
                {
                    Console.WriteLine("❌ [keymap_worker] 'keymaps' is not an array.");
                    return;
                }

                foreach (var keymapEntry in keymapsArray.AsArray)
                {
                    if (_stopRequested)
                        return; // graceful exit

                    if (!keymapEntry.IsDocument)
                        continue;

                    var doc = keymapEntry.AsDocument;
                    if (doc.ContainsKey(defaultKeymap))
                    {
                        var defaultKeymapData = doc[defaultKeymap];
                        string json = defaultKeymapData.ToString();

                        if (string.IsNullOrWhiteSpace(json))
                        {
                            // Console.WriteLine($"⚠️ [keymap_worker] Default keymap '{defaultKeymap}' is empty.");
                            return;
                        }

                        // ✅ Dispatch to UI thread for safe access
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // Console.WriteLine($"✅ [UI Thread] Loaded keymap: {defaultKeymap}");
                            // Console.WriteLine(json);

                            // ✅ Pass JSON to global KeyMapManager
                            KeyMapManager.LoadKeymap(json);

                            // Optionally trigger overlay update if you want:
                            OverlayManager.Instance?.rerender_overlay();
                        }));

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [keymap_worker] Error: {ex.Message}");
            }
        }

        // ✅ Graceful stop
        public void StopWorker()
        {
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _stopRequested = true;
                _workerThread.Join(1000); // wait up to 1s
            }

            _workerThread = null;
        }

        // ✅ Restart helper (used when default keymap changes)
        public void Restart()
        {
            Console.WriteLine("🔁 Restarting keymap worker...");
            StopWorker();
            StartWorker();
        }
    }
}
