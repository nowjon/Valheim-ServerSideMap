using System.Collections;
using System.IO;
using UnityEngine;

namespace ServerSideMap
{
    public class WebMapInitializer : MonoBehaviour
    {
        private WebMapServer _webMapServer;
        private GameObject _updateObject;

        public void Initialize()
        {
            StartCoroutine(InitializeWebMap());
        }

        private IEnumerator InitializeWebMap()
        {
            // Wait for ZNet to be ready
            yield return new WaitForSeconds(2f);

            if (!_ZNet.IsServer(_ZNet._instance))
            {
                Utility.Log("WebMap: Not running on server, skipping initialization");
                Destroy(gameObject);
                yield break;
            }

            // Get the plugin directory - try multiple methods
            string webMapDirectory = null;
            
            // Method 1: Use BepInEx.Paths.PluginPath
            try
            {
                var pluginPath = BepInEx.Paths.PluginPath;
                webMapDirectory = Path.Combine(pluginPath, "ServerSideMap", "WebMap");
            }
            catch { }
            
            // Method 2: Use assembly location
            if (webMapDirectory == null || !Directory.Exists(webMapDirectory))
            {
                try
                {
                    var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                    webMapDirectory = Path.Combine(assemblyDir, "WebMap");
                }
                catch { }
            }
            
            // Method 3: Fallback to a default location
            if (webMapDirectory == null || !Directory.Exists(webMapDirectory))
            {
                webMapDirectory = Path.Combine(Application.dataPath, "..", "BepInEx", "plugins", "ServerSideMap", "WebMap");
            }

            if (!Directory.Exists(webMapDirectory))
            {
                Directory.CreateDirectory(webMapDirectory);
                Utility.Log($"WebMap: Created directory {webMapDirectory}");
            }

            PlayerTracker.SetUpdateInterval(Store.WebMapPlayerUpdateInterval.Value);

            _webMapServer = new WebMapServer();
            _webMapServer.Start(Store.WebMapPort.Value, webMapDirectory);

            // Create update object for player tracking
            _updateObject = new GameObject("WebMapUpdater");
            UnityEngine.Object.DontDestroyOnLoad(_updateObject);
            var updater = _updateObject.AddComponent<WebMapUpdater>();
            updater.Initialize();

            Utility.Log($"WebMap: Server initialized on port {Store.WebMapPort.Value}");
        }

        void OnDestroy()
        {
            _webMapServer?.Stop();
            if (_updateObject != null)
            {
                UnityEngine.Object.Destroy(_updateObject);
            }
        }
    }
}

