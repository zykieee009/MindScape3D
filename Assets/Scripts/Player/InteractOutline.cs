using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A soft blue glow around something you can interact with, used in place of the floating gold marker.
// Every renderer under this object (props and animated characters alike) gets two hull copies drawn with the
// "Mindscape/Interact Outline" and "... Halo" shaders (Resources/InteractOutline and InteractOutlineHalo materials):
// a crisp blue edge fading out to a faint halo, just outside the silhouette. Shown while the thing is available;
// stronger while it is the one [E] will act on.
public class InteractOutline : MonoBehaviour
{
    const float Idle = 1f, Focused = 1.6f;
    static readonly int CenterId = Shader.PropertyToID("_CenterWS"), IntensityId = Shader.PropertyToID("_Intensity");

    static Material edgeTemplate, haloTemplate;

    readonly List<Renderer> sources = new List<Renderer>();
    readonly List<Renderer> hulls = new List<Renderer>();
    Material edge, halo;
    bool built, shown, focused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { edgeTemplate = haloTemplate = null; }

    public void Show(bool on, bool isFocused = false)
    {
        if (!built) Build();
        if (edge == null) return;
        if (on != shown) { shown = on; foreach (var h in hulls) if (h != null) h.enabled = on; }
        if (on && isFocused != focused)
        {
            focused = isFocused;
            edge.SetFloat(IntensityId, focused ? Focused : Idle);
            halo.SetFloat(IntensityId, focused ? Focused : Idle);
        }
        if (on) UpdateCenter();
    }

    void LateUpdate()
    {
        if (shown) UpdateCenter();
    }

    // The glow grows away from the middle of the object on screen.
    void UpdateCenter()
    {
        bool any = false; Bounds b = default;
        foreach (var r in sources)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        edge.SetVector(CenterId, b.center);
        halo.SetVector(CenterId, b.center);
    }

    void Build()
    {
        built = true;
        if (edgeTemplate == null) edgeTemplate = Resources.Load<Material>("InteractOutline");
        if (haloTemplate == null) haloTemplate = Resources.Load<Material>("InteractOutlineHalo");
        if (edgeTemplate == null || haloTemplate == null) { Debug.LogWarning("InteractOutline: Resources/InteractOutline(Halo) material missing."); return; }
        edge = new Material(edgeTemplate); halo = new Material(haloTemplate);
        foreach (var source in GetComponentsInChildren<Renderer>(true))
        {
            if (source.name.StartsWith("Interact outline") || !source.enabled) continue;
            Mesh mesh = source is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : source is MeshRenderer && source.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh == null) continue;
            sources.Add(source);
            hulls.Add(Hull(source, mesh, edge, "Interact outline"));
            hulls.Add(Hull(source, mesh, halo, "Interact outline halo"));
        }
    }

    Renderer Hull(Renderer source, Mesh mesh, Material material, string name)
    {
        var go = new GameObject(name);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        Renderer hull;
        if (source is SkinnedMeshRenderer skinned)
        {
            var copy = go.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = mesh; copy.bones = skinned.bones; copy.rootBone = skinned.rootBone;
            copy.localBounds = skinned.localBounds; copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
            hull = copy;
        }
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            hull = go.AddComponent<MeshRenderer>();
        }
        var mats = new Material[mesh.subMeshCount];
        for (int i = 0; i < mats.Length; i++) mats[i] = material;
        hull.sharedMaterials = mats;
        hull.shadowCastingMode = ShadowCastingMode.Off; hull.receiveShadows = false;
        hull.lightProbeUsage = LightProbeUsage.Off; hull.reflectionProbeUsage = ReflectionProbeUsage.Off;
        hull.enabled = shown;
        return hull;
    }

    void OnDestroy()
    {
        if (edge != null) Destroy(edge);
        if (halo != null) Destroy(halo);
    }
}
