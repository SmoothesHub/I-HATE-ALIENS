using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IHateAliens
{
    internal sealed class ShipDiscovery : IDisposable
    {
        private sealed class SceneRoots
        {
            internal Scene Scene;
            internal readonly List<GameObject> Roots = new List<GameObject>();
        }

        private readonly List<SceneRoots> scenes = new List<SceneRoots>();
        private readonly Dictionary<Transform, HierarchyWatch> watches = new Dictionary<Transform, HierarchyWatch>();
        private readonly Dictionary<Transform, ShipSuppression> ships = new Dictionary<Transform, ShipSuppression>();
        private readonly List<Transform> stale = new List<Transform>();
        private readonly string[][] routes;
        private readonly Action<string> log;
        private bool dirty = true;
        private bool disposed;
        internal int LocationPasses { get; private set; }

        internal ShipDiscovery(Action<string> log)
        {
            this.log = log;
            routes = Array.ConvertAll(ShipSignature.Locations, p => p.Split('/'));
        }

        internal void Request() { dirty = true; }

        // Called twice per second. Stable worlds check only scene/root counts and cached
        // ship components. The rest of the map is never enumerated.
        internal void Tick()
        {
            if (disposed) return;
            RefreshScenes();
            if (dirty)
            {
                dirty = false;
                LocationPasses++;
                foreach (SceneRoots scene in scenes)
                    foreach (GameObject root in scene.Roots)
                    {
                        if (root == null) continue;
                        foreach (string[] route in routes)
                            if (ShipSignature.OriginalName(root.name) == route[0]) Follow(root.transform, route, 1);
                    }
            }
            foreach (ShipSuppression ship in ships.Values) ship.Enforce();
            Prune();
        }

        private void RefreshScenes()
        {
            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                SceneRoots cached = scenes[i];
                if (!cached.Scene.IsValid() || !cached.Scene.isLoaded)
                { scenes.RemoveAt(i); dirty = true; continue; }
                bool changed = cached.Scene.rootCount != cached.Roots.Count;
                if (!changed)
                    foreach (GameObject root in cached.Roots)
                        if (root == null || root.transform.parent != null || root.scene != cached.Scene)
                        { changed = true; break; }
                if (changed)
                {
                    cached.Roots.Clear();
                    cached.Scene.GetRootGameObjects(cached.Roots);
                    dirty = true;
                }
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                bool known = false;
                foreach (SceneRoots cached in scenes) if (cached.Scene == scene) { known = true; break; }
                if (known) continue;
                var added = new SceneRoots { Scene = scene };
                scene.GetRootGameObjects(added.Roots);
                scenes.Add(added);
                dirty = true;
            }
        }

        private void Follow(Transform current, string[] route, int next)
        {
            Watch(current);
            if (next == route.Length)
            {
                if (ships.ContainsKey(current)) return;
                if (ShipSignature.Matches(current))
                {
                    ships.Add(current, new ShipSuppression(current));
                    log?.Invoke("Disabled verified sky ship in " + current.gameObject.scene.name + ".");
                }
                else
                {
                    // A streamed prefab can arrive in pieces. Observe just its known assembly points.
                    Watch(current.Find("AlienMothership_FBX"));
                    Watch(current.Find("AlienMothership_FBX/AlienMothership_VFX"));
                    Watch(current.Find("AlienMothership_FBX/AlienMothership_VFX/Rays1"));
                }
                return;
            }
            // Direct children only; no recursive scene traversal or GameObject.Find.
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (ShipSignature.OriginalName(child.name) == route[next]) Follow(child, route, next + 1);
            }
        }

        private void Watch(Transform node)
        {
            if (node == null || watches.ContainsKey(node)) return;
            HierarchyWatch watch = node.gameObject.AddComponent<HierarchyWatch>();
            watch.Changed = Request;
            watches.Add(node, watch);
        }

        private void Prune()
        {
            stale.Clear();
            foreach (var pair in watches) if (pair.Key == null) stale.Add(pair.Key);
            foreach (Transform key in stale) watches.Remove(key);
            stale.Clear();
            foreach (var pair in ships) if (!pair.Value.HasSurvivingObjects) stale.Add(pair.Key);
            foreach (Transform key in stale) { ships[key].Release(); ships.Remove(key); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (HierarchyWatch watch in watches.Values)
                if (watch != null) { watch.Changed = null; UnityEngine.Object.Destroy(watch); }
            foreach (ShipSuppression ship in ships.Values) ship.Release();
            watches.Clear(); ships.Clear(); scenes.Clear();
        }
    }

    public sealed class HierarchyWatch : MonoBehaviour
    {
        internal Action Changed;
        private void OnEnable() { Changed?.Invoke(); }
        private void OnTransformChildrenChanged() { Changed?.Invoke(); }
        private void OnTransformParentChanged() { Changed?.Invoke(); }
    }
}
