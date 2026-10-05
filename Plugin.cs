using System;
using System.Collections;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IHateAliens
{
    [BepInPlugin("com.eldon.ihatealiens", "I HATE ALIENS", "1.0.1")]
    [BepInProcess("Gorilla Tag.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ShipDiscovery discovery;
        private float retryAt;
        private int retries;
        private bool warned;

        private void OnEnable()
        {
            discovery = new ShipDiscovery(message => Logger.LogInfo(message));
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ZoneManagement.OnZoneChange += OnZoneChanged;
            RequestChecks();
            TickSafely();
            StartCoroutine(Watch());
            Logger.LogInfo("I HATE ALIENS enabled; using verified ship locations and mesh signatures.");
        }

        private IEnumerator Watch()
        {
            var delay = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                yield return delay;
                if (retries > 0 && Time.realtimeSinceStartup >= retryAt)
                {
                    discovery.Request();
                    retries--;
                    retryAt = Time.realtimeSinceStartup + (retries == 2 ? 1.5f : 6f);
                }
                TickSafely();
            }
        }

        private void RequestChecks()
        {
            discovery?.Request();
            retries = 3;
            retryAt = Time.realtimeSinceStartup + 0.5f;
        }

        private void TickSafely()
        {
            try { discovery?.Tick(); }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Logger.LogWarning("Ship check failed. Unidentified objects are left unchanged. " + e);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { RequestChecks(); TickSafely(); }
        private void OnSceneUnloaded(Scene scene) { RequestChecks(); }
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) { RequestChecks(); }
        private void OnZoneChanged(ZoneData[] zones) { RequestChecks(); TickSafely(); }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ZoneManagement.OnZoneChange -= OnZoneChanged;
            StopAllCoroutines();
            discovery?.Dispose();
            discovery = null;
        }
    }
}
