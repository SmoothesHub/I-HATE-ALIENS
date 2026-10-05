using System;
using System.Collections.Generic;
using UnityEngine;

namespace IHateAliens
{
    // Lives on the persistent plugin object, so an inactive ship cannot stop the watchdog.
    internal sealed class ShipSuppression
    {
        internal readonly Transform Root;
        private readonly Dictionary<Component, Action> restore = new Dictionary<Component, Action>();
        private readonly List<Component> owned = new List<Component>();
        private readonly List<ShipGuard> guards = new List<ShipGuard>();
        private readonly List<Component> scratch = new List<Component>();
        private readonly Dictionary<Transform, int> childCounts = new Dictionary<Transform, int>();
        private readonly bool wasActive;
        private bool busy;
        private bool released;
        internal bool Dirty = true;

        internal bool HasSurvivingObjects
        {
            get { foreach (Component c in owned) if (c != null) return true; return Root != null; }
        }

        internal ShipSuppression(Transform root)
        {
            Root = root;
            wasActive = root.gameObject.activeSelf;
            Enforce();
        }

        internal void Enforce()
        {
            if (released || busy) return;
            busy = true;
            try
            {
                // Unity can omit hierarchy callbacks on inactive objects. Check only the
                // already owned transforms, so new nested effects are still discovered.
                foreach (var pair in childCounts)
                    if (pair.Key != null && pair.Key.childCount != pair.Value) { Dirty = true; break; }
                if (Dirty && Root != null)
                {
                    Dirty = false;
                    scratch.Clear();
                    Root.GetComponentsInChildren(true, scratch);
                    foreach (Component c in scratch)
                    {
                        if (c == null || c is ShipGuard || restore.ContainsKey(c)) continue;
                        Capture(c);
                        if (c is Transform t)
                        {
                            ShipGuard guard = t.gameObject.AddComponent<ShipGuard>();
                            guard.Owner = this;
                            guards.Add(guard);
                        }
                    }
                    childCounts.Clear();
                    foreach (Component c in owned)
                        if (c != null && c is Transform t) childCounts[t] = t.childCount;
                }
                // Disable producers before clearing their output. All components were captured
                // from the proven ship subtree, never from a global audio/particle search.
                foreach (Component c in owned)
                    if (c != null && c is Behaviour b && !(c is AudioSource) && b.enabled) b.enabled = false;
                foreach (Component c in owned) if (c != null) Quench(c);
                if (Root != null && Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
            }
            finally { busy = false; }
        }

        private void Capture(Component c)
        {
            Action undo = () => { };
            if (c is AudioSource a)
            {
                bool enabled = a.enabled, mute = a.mute, awake = a.playOnAwake;
                float volume = a.volume;
                bool playing = a.isPlaying;
                undo = () => { a.enabled = enabled; a.mute = mute; a.volume = volume; a.playOnAwake = awake;
                    if (playing && a.isActiveAndEnabled) a.Play(); };
            }
            else if (c is Behaviour b)
            {
                bool enabled = b.enabled;
                undo = () => b.enabled = enabled;
            }
            else if (c is Renderer r)
            {
                bool enabled = r.enabled;
                undo = () => r.enabled = enabled;
            }
            else if (c is ParticleSystem p)
            {
                bool emission = p.emission.enabled, awake = p.main.playOnAwake, playing = p.isPlaying;
                ParticleSystemStopAction stopAction = p.main.stopAction;
                undo = () => { var em = p.emission; em.enabled = emission; var main = p.main;
                    main.playOnAwake = awake; main.stopAction = stopAction;
                    if (playing && p.gameObject.activeInHierarchy) p.Play(false); };
            }
            restore.Add(c, undo);
            owned.Add(c);
        }

        private static void Quench(Component c)
        {
            if (c is AudioSource a)
            {
                if (!a.mute) a.mute = true;
                if (a.volume != 0f) a.volume = 0f;
                if (a.playOnAwake) a.playOnAwake = false;
                if (a.isPlaying) a.Stop(); // also stops one-shots on this source
                if (a.enabled) a.enabled = false;
            }
            else if (c is Renderer r)
            {
                if (r.enabled) r.enabled = false;
                if (r is TrailRenderer trail && trail.positionCount > 0) trail.Clear();
            }
            else if (c is ParticleSystem p)
            {
                var main = p.main;
                // Stop must never run an authored Destroy/callback stop action.
                if (main.stopAction != ParticleSystemStopAction.None) main.stopAction = ParticleSystemStopAction.None;
                if (main.playOnAwake) main.playOnAwake = false;
                var em = p.emission;
                if (em.enabled) em.enabled = false;
                if (p.isPlaying || p.isPaused || p.particleCount != 0)
                    p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        internal void Release()
        {
            if (released) return;
            released = true;
            foreach (ShipGuard guard in guards)
                if (guard != null) { guard.Owner = null; UnityEngine.Object.Destroy(guard); }
            // Restore producers last, after their outputs are restored.
            foreach (Component c in owned)
                if (c != null && !(c is Behaviour)) restore[c]();
            foreach (Component c in owned)
                if (c != null && c is Behaviour) restore[c]();
            if (Root != null) Root.gameObject.SetActive(wasActive);
            restore.Clear(); owned.Clear(); guards.Clear(); childCounts.Clear();
        }
    }

    // No Update methods. These callbacks run only when a ship object changes.
    [DefaultExecutionOrder(-32000)]
    public sealed class ShipGuard : MonoBehaviour
    {
        internal ShipSuppression Owner;
        private void OnEnable() { if (Owner != null) { Owner.Dirty = true; Owner.Enforce(); } }
        // Defer capture until the watchdog: SetParent can run before the producer
        // attaches its particle/light/audio components to the new child.
        private void OnTransformChildrenChanged() { if (Owner != null) Owner.Dirty = true; }
        private void OnTransformParentChanged() { if (Owner != null) Owner.Enforce(); }
    }
}
