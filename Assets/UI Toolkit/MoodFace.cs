using UnityEngine;
using UnityEngine.UIElements;

// An emoji-style face for the stress check-in cards, drawn as vectors so it looks the same everywhere (the game's
// fonts have no emoji, and a WebGL build has no system emoji font). Level: 1 calm, 2 a little tense, 3 stressed,
// 4 very stressed, 5 overwhelmed. Usable in UXML / UI Builder (Library > Project > Custom Controls).
[UxmlElement]
public partial class MoodFace : VisualElement
{
    int level = 1;

    [UxmlAttribute] public int Level { get => level; set { level = Mathf.Clamp(value, 1, 5); MarkDirtyRepaint(); } }

    public MoodFace() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
    public MoodFace(int level) : this() { Level = level; }

    void Draw(MeshGenerationContext ctx)
    {
        float s = Mathf.Min(contentRect.width, contentRect.height);
        if (s <= 0) return;
        var p = ctx.painter2D;
        var origin = new Vector2((contentRect.width - s) * .5f, (contentRect.height - s) * .5f);
        Vector2 V(float x, float y) => origin + new Vector2(x * s, y * s);
        var ink = new Color(.33f, .2f, .07f);
        void Line(params float[] c) { p.BeginPath(); p.MoveTo(V(c[0], c[1])); for (int i = 2; i < c.Length; i += 2) p.LineTo(V(c[i], c[i + 1])); p.Stroke(); }
        void Arc(float x, float y, float r, float from, float to) { p.BeginPath(); p.Arc(V(x, y), r * s, from, to); p.Stroke(); }
        void Dot(float x, float y, float r, Color color) { p.fillColor = color; p.BeginPath(); p.Arc(V(x, y), r * s, 0, 360); p.Fill(); }

        // The face, a little warmer as the level rises.
        Dot(.5f, .5f, .46f, Color.Lerp(new Color(1f, .84f, .33f), new Color(1f, .72f, .3f), (level - 1) / 4f));
        p.strokeColor = new Color(.85f, .58f, .16f); p.lineWidth = .035f * s;
        Arc(.5f, .5f, .46f, 0, 360);

        p.strokeColor = ink; p.lineWidth = .055f * s; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
        switch (level)
        {
            case 1: // calm: closed, content eyes, a soft smile and rosy cheeks
                Dot(.27f, .6f, .07f, new Color(1f, .55f, .45f, .45f)); Dot(.73f, .6f, .07f, new Color(1f, .55f, .45f, .45f));
                Arc(.35f, .4f, .085f, 20, 160); Arc(.65f, .4f, .085f, 20, 160);
                Arc(.5f, .55f, .19f, 30, 150);
                break;
            case 2: // a little tense: open eyes, a wobbly line of a mouth
                Dot(.36f, .42f, .05f, ink); Dot(.64f, .42f, .05f, ink);
                p.BeginPath(); p.MoveTo(V(.34f, .7f)); p.BezierCurveTo(V(.42f, .64f), V(.52f, .74f), V(.66f, .66f)); p.Stroke();
                break;
            case 3: // stressed: worried brows and a frown
                Line(.25f, .32f, .41f, .26f); Line(.75f, .32f, .59f, .26f);
                Dot(.36f, .44f, .05f, ink); Dot(.64f, .44f, .05f, ink);
                Arc(.5f, .86f, .19f, 220, 320);
                break;
            case 4: // very stressed: squeezed eyes and a clenched, zig-zag mouth
                Line(.27f, .34f, .41f, .41f, .27f, .48f); Line(.73f, .34f, .59f, .41f, .73f, .48f);
                Line(.31f, .7f, .37f, .64f, .44f, .7f, .5f, .64f, .56f, .7f, .63f, .64f, .69f, .7f);
                break;
            default: // overwhelmed: raised brows, wide eyes, an open mouth and a bead of sweat
                Line(.24f, .27f, .4f, .22f); Line(.76f, .27f, .6f, .22f);
                Arc(.36f, .42f, .085f, 0, 360); Arc(.64f, .42f, .085f, 0, 360);
                Dot(.36f, .43f, .035f, ink); Dot(.64f, .43f, .035f, ink);
                p.fillColor = ink; p.BeginPath(); p.MoveTo(V(.5f, .58f));
                p.BezierCurveTo(V(.56f, .58f), V(.6f, .64f), V(.6f, .7f)); p.BezierCurveTo(V(.6f, .76f), V(.56f, .82f), V(.5f, .82f));
                p.BezierCurveTo(V(.44f, .82f), V(.4f, .76f), V(.4f, .7f)); p.BezierCurveTo(V(.4f, .64f), V(.44f, .58f), V(.5f, .58f)); p.ClosePath(); p.Fill();
                p.fillColor = new Color(.45f, .75f, 1f); p.BeginPath(); p.MoveTo(V(.84f, .14f));
                p.BezierCurveTo(V(.9f, .24f), V(.93f, .3f), V(.88f, .34f)); p.BezierCurveTo(V(.83f, .38f), V(.77f, .32f), V(.8f, .25f)); p.ClosePath(); p.Fill();
                break;
        }
    }
}
