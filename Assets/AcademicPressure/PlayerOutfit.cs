using System.Linq;
using UnityEngine;

namespace Mindscape.AcademicPressure
{
    public enum Outfit { Home, SchoolUniform, GoingOut }

    // What Joey wears. The rig has one shirt and one pair of shorts; an outfit recolours them with a property block,
    // so the shared materials (and every other scene) are left alone. Home is the model's own lagoon-teal shirt.
    // Changing happens in the bathroom (ChangeClothes in AcademicEpisode); later episodes start already dressed.
    public static class PlayerOutfit
    {
        static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");   // glTF shader graph
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");              // URP Lit

        // Shirt, then shorts.
        static Color[] Colours(Outfit o) => o switch
        {
            Outfit.SchoolUniform => new[] { new Color(.93f, .93f, .9f), new Color(.1f, .13f, .25f) },   // white polo, navy
            Outfit.GoingOut => new[] { new Color(.86f, .6f, .24f), new Color(.17f, .22f, .36f) },        // mustard shirt, denim
            _ => null,
        };

        // The outfit an episode starts in: school days in uniform, the reunion in going-out clothes.
        // Chapter 1 Episode 1 and Chapter 3 Episode 1 start at home and change in the bathroom.
        public static Outfit ForEpisode(int chapter, int episode)
        {
            if (chapter == 1) return episode >= 2 ? Outfit.SchoolUniform : Outfit.Home;
            if (chapter == 2) return episode <= 3 ? Outfit.SchoolUniform : Outfit.Home;
            if (chapter == 3) return episode >= 2 && episode <= 7 ? Outfit.GoingOut : Outfit.Home;
            return Outfit.Home;
        }

        public static void Wear(Component who, Outfit outfit)
        {
            if (who == null) return;
            var colours = Colours(outfit);
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    int part = mats[i].name.StartsWith("Shirt") ? 0 : mats[i].name.StartsWith("Shorts") ? 1 : -1;
                    if (part < 0) continue;
                    var block = new MaterialPropertyBlock();
                    if (colours != null) { block.SetColor(BaseColorFactor, colours[part]); block.SetColor(BaseColor, colours[part]); }
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        // The bathroom: the room behind the house's Door_Bathroom. Returns a point on its floor, a little way in.
        // The bathroom is the closed side of the door (the hallway side opens much further).
        public static bool Bathroom(out Vector3 inside, out Vector3 door)
        {
            inside = door = Vector3.zero;
            var d = Object.FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Door_Bathroom" && x.isActiveAndEnabled);
            if (d == null) return false;
            door = d.transform.position;
            var leaf = d.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Leaf")).Select(r => r.bounds).FirstOrDefault();
            Vector3 across = leaf.size == Vector3.zero || leaf.size.z < leaf.size.x ? Vector3.forward : Vector3.right;
            Vector3 eye = door + Vector3.up * 1.6f;
            float Open(Vector3 dir) => Physics.Raycast(eye + dir * .6f, dir, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 30;
            Vector3 into = Open(across) < Open(-across) ? across : -across;
            float depth = Mathf.Clamp(Open(into) * .45f, 1.4f, 2.6f);
            inside = door + into * depth;
            if (Physics.Raycast(inside + Vector3.up * 3, Vector3.down, out var floor, 6, ~0, QueryTriggerInteraction.Ignore)) inside.y = floor.point.y;
            return true;
        }
    }
}
