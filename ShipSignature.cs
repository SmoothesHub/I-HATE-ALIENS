using System;
using UnityEngine;

namespace IHateAliens
{
    // These identities were read from Steam build 25664145. No substring matching.
    internal static class ShipSignature
    {
        internal static readonly string[] Locations =
        {
            "Environment Objects/LocalObjects_Prefab/Forest/2026_Halloween_Forest/Terraformer/AlienMothership_Forest",
            "Canyon/2026_Halloween_Canyon/Terraformers/AlienMothership_Canyon",
            "Beach/2026_Halloween_Beach/Terraformers/AlienMothership_Beach",
            "Mountain/2026_Halloween_Mountain/Terraformers/AlienMothership_Mountain",
            "skyjungle/2025_Halloween1_Skyjungle/Terraformers/AlienMothership",
            "MetroMain/2026_Halloween1_Metro/Terraformers/AlienMothership_Metro",
            "GTFC Scene/Vista/MetroMain/2026_Halloween1_Metro/Terraformers/AlienMothership_Metro",
            "City_Pretty/Event_Overview_AlienArrival/10_02_ArrivalExperience/Arrival_Scene/2WorldRelativeObjects/EventHierarchy/6SubAnimation/AlienMothership"
        };

        internal static string OriginalName(string name)
        {
            const string clone = "(Clone)";
            return name.EndsWith(clone, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - clone.Length) : name;
        }

        internal static bool HasKnownLocation(Transform root)
        {
            foreach (string location in Locations)
            {
                string[] names = location.Split('/');
                Transform cursor = root;
                int i = names.Length - 1;
                while (cursor != null && i >= 0 && OriginalName(cursor.name) == names[i])
                {
                    cursor = cursor.parent;
                    i--;
                }
                if (i < 0 && cursor == null) return true;
            }
            return false;
        }

        internal static bool Matches(Transform root)
        {
            if (root == null || !root.gameObject.scene.IsValid() || !HasKnownLocation(root)) return false;
            Transform model = root.Find("AlienMothership_FBX");
            if (model == null || model.GetComponent<Animator>() == null) return false;
            // Four independently named meshes and their topology/bounds distinguish the
            // photographed ship from props, cosmetics, and similarly named update objects.
            if (!MeshMatches(model, "AlienMothership", 830, new Vector3(23.71101f, 8.075985f, 23.71101f))) return false;
            if (!MeshMatches(model, "AlienMotherEyeball", 414, Vector3.one * 10.270958f)) return false;
            if (!MeshMatches(model, "AlienMotherLid_BOT", 119, new Vector3(5.200967f, 10.270958f, 5.135479f))) return false;
            if (!MeshMatches(model, "AlienMotherLid_TOP", 129, new Vector3(5.200964f, 10.270958f, 5.135479f))) return false;
            Transform effects = model.Find("AlienMothership_VFX");
            Transform rays = effects != null ? effects.Find("Rays1") : null;
            Transform sound = rays != null ? rays.Find("MothershipRayAudio") : null;
            if (effects == null || effects.GetComponent<ParticleSystem>() == null ||
                rays == null || rays.GetComponent<ParticleSystem>() == null ||
                sound == null || sound.GetComponent<AudioSource>() == null) return false;

            // Require the world-sized ship, not a miniature copied into the hierarchy.
            Transform hull = model.Find("AlienMothership");
            Vector3 scale = hull.lossyScale;
            return Mathf.Abs(scale.x) * 23.71101f >= 50f && Mathf.Abs(scale.z) * 23.71101f >= 50f;
        }

        private static bool MeshMatches(Transform model, string name, int vertices, Vector3 size)
        {
            Transform part = model.Find(name);
            if (part == null || part.GetComponent<MeshRenderer>() == null) return false;
            MeshFilter filter = part.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.name != name || mesh.vertexCount != vertices) return false;
            return (mesh.bounds.size - size).sqrMagnitude < 0.0001f;
        }
    }
}
