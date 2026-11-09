using UnityEditor;
using UnityEngine;

// The NPC roster (Blender Files/NPC_Roster.blend) was exported with the hair's faces pointing into the head.
// The GLB copies hide it (glTF materials are double-sided), but the FBX materials cull back faces, so the hair
// vanishes except for a thin rim. On import, turn the hair outward again. It only flips hair that mostly faces
// inward, so a later fix in Blender is left alone.
public class NPCHairNormalsFix : AssetPostprocessor
{
    const string Folder = "Assets/Characters/NPCs/";

    public override uint GetVersion() => 1;

    void OnPostprocessModel(GameObject model)
    {
        if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".fbx")) return;
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = renderer.sharedMesh; var materials = renderer.sharedMaterials;
            if (mesh == null) continue;
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                if (materials[s] != null && materials[s].name.Contains("Hair") && FacesInward(mesh, s)) Flip(mesh, s);
        }
    }

    static bool FacesInward(Mesh mesh, int submesh)
    {
        var v = mesh.vertices; var t = mesh.GetTriangles(submesh);
        if (t.Length == 0) return false;
        Vector3 center = Vector3.zero; foreach (int i in t) center += v[i]; center /= t.Length;
        int outward = 0;
        for (int k = 0; k < t.Length; k += 3)
        {
            Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - center) > 0) outward++;
        }
        return outward * 3 < t.Length * .3f;
    }

    static void Flip(Mesh mesh, int submesh)
    {
        var t = mesh.GetTriangles(submesh);
        for (int k = 0; k < t.Length; k += 3) (t[k + 1], t[k + 2]) = (t[k + 2], t[k + 1]);
        mesh.SetTriangles(t, submesh, false);
        var normals = mesh.normals; var tangents = mesh.tangents; bool hasTangents = tangents.Length == normals.Length;
        foreach (int i in new System.Collections.Generic.HashSet<int>(t))
        {
            normals[i] = -normals[i];
            if (hasTangents) tangents[i] = new Vector4(-tangents[i].x, -tangents[i].y, -tangents[i].z, tangents[i].w);
        }
        mesh.normals = normals;
        if (hasTangents) mesh.tangents = tangents;
    }
}
