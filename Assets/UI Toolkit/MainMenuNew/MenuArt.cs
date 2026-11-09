using UnityEngine;
using UnityEngine.UIElements;

// The main menu's two drawn elements, usable in UXML / UI Builder (Library > Project > Custom Controls).

// Native paper fibers and inset page rules echo the illustrated reference books. Put one as the first child of a
// .paper element; it fills it and never takes clicks.
[UxmlElement]
public partial class PaperGrain : VisualElement
{
    public PaperGrain()
    {
        pickingMode = PickingMode.Ignore; style.position = Position.Absolute;
        style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
        generateVisualContent += ctx =>
        {
            float w = contentRect.width, h = contentRect.height; if (w < 1 || h < 1) return;
            var p = ctx.painter2D; var random = new System.Random(43);
            p.lineWidth = 1;
            for (int i = 0; i < 1200; i++)
            {
                float x = (float)random.NextDouble() * w, y = (float)random.NextDouble() * h;
                p.strokeColor = new Color(.38f, .28f, .14f, .035f + (float)random.NextDouble() * .025f);
                p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + 1 + (float)random.NextDouble() * 6, y + .3f)); p.Stroke();
            }
            p.strokeColor = new Color(.4f, .3f, .18f, .16f);
            p.BeginPath(); p.MoveTo(new Vector2(7, 7)); p.LineTo(new Vector2(w - 7, 7)); p.LineTo(new Vector2(w - 7, h - 7)); p.LineTo(new Vector2(7, h - 7)); p.ClosePath(); p.Stroke();
        };
    }
}

// Small native vector illustrations stay crisp at every display scale. Kind: sprig, book, power, lock, help,
// settings, phone, people, puzzle, pen, play, refresh, eye, eye-off or leaf (anything else draws the leaf). Dark: ink lines for paper; light
// (default) for the dark background.
[UxmlElement]
public partial class MenuLineArt : VisualElement
{
    string kind = "leaf";
    bool dark;

    [UxmlAttribute] public string Kind { get => kind; set { kind = value; MarkDirtyRepaint(); } }
    [UxmlAttribute] public bool Dark { get => dark; set { dark = value; MarkDirtyRepaint(); } }

    public MenuLineArt() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
    public MenuLineArt(string kind, bool dark) : this() { this.kind = kind; this.dark = dark; }

    void Draw(MeshGenerationContext ctx)
    {
        var p = ctx.painter2D; float w = contentRect.width, h = contentRect.height;
        if (w <= 0 || h <= 0) return;
        p.strokeColor = dark ? new Color(.34f, .29f, .2f) : new Color(.88f, .79f, .61f); p.lineWidth = Mathf.Max(1.3f, Mathf.Min(w, h) * .055f);
        Vector2 V(float x, float y) => new Vector2(x * w, y * h);
        void Line(params float[] coords) { p.BeginPath(); p.MoveTo(V(coords[0], coords[1])); for (int i = 2; i < coords.Length; i += 2) p.LineTo(V(coords[i], coords[i + 1])); p.Stroke(); }
        void Circle(float x, float y, float r) { p.BeginPath(); p.Arc(V(x, y), r * Mathf.Min(w, h), 0, 360); p.Stroke(); }
        if (kind == "eye" || kind == "eye-off")
        {
            Line(.06f, .5f, .25f, .29f, .5f, .21f, .75f, .29f, .94f, .5f, .75f, .71f, .5f, .79f, .25f, .71f, .06f, .5f); Circle(.5f, .5f, .14f);
            if (kind == "eye-off") Line(.14f, .86f, .86f, .14f);
            return;
        }
        if (kind == "sprig") { Line(0, .5f, 1, .5f); Line(.4f, .5f, .49f, .3f, .56f, .5f, .49f, .7f, .4f, .5f); return; }
        if (kind == "book") { Line(.5f, .22f, .5f, .88f); Line(.5f, .25f, .3f, .15f, .08f, .15f, .08f, .78f, .3f, .78f, .5f, .88f, .7f, .78f, .92f, .78f, .92f, .15f, .7f, .15f, .5f, .25f); return; }
        if (kind == "power") { p.BeginPath(); p.Arc(V(.5f, .55f), .34f * Mathf.Min(w, h), -55, 235); p.Stroke(); Line(.5f, .05f, .5f, .5f); return; }
        if (kind == "lock") { Line(.2f, .46f, .8f, .46f, .8f, .9f, .2f, .9f, .2f, .46f); p.BeginPath(); p.Arc(V(.5f, .4f), .23f * Mathf.Min(w, h), 180, 360); p.Stroke(); Line(.27f, .4f, .27f, .46f); Line(.73f, .4f, .73f, .46f); Line(.5f, .62f, .5f, .75f); return; }
        if (kind == "help") { Circle(.5f, .5f, .4f); Line(.37f, .35f, .44f, .27f, .6f, .29f, .64f, .4f, .5f, .53f, .5f, .61f); Circle(.5f, .75f, .025f); return; }
        if (kind == "settings") { Circle(.5f, .5f, .28f); Circle(.5f, .5f, .1f); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Line(.5f + Mathf.Cos(a) * .3f, .5f + Mathf.Sin(a) * .3f, .5f + Mathf.Cos(a) * .44f, .5f + Mathf.Sin(a) * .44f); } return; }
        if (kind == "phone") { Line(.25f, .12f, .12f, .22f, .17f, .43f, .37f, .68f, .65f, .88f, .82f, .87f, .9f, .7f, .72f, .57f, .6f, .66f, .34f, .4f, .42f, .27f, .25f, .12f); return; }
        if (kind == "people") { Circle(.34f, .3f, .14f); Circle(.73f, .35f, .12f); Line(.08f, .88f, .12f, .64f, .3f, .54f, .5f, .62f, .56f, .88f); Line(.6f, .58f, .83f, .59f, .93f, .88f); return; }
        if (kind == "puzzle") { Line(.15f, .15f, .42f, .15f, .42f, .26f); Circle(.5f, .3f, .08f); Line(.58f, .26f, .58f, .15f, .85f, .15f, .85f, .42f, .74f, .42f); Circle(.7f, .5f, .08f); Line(.74f, .58f, .85f, .58f, .85f, .85f, .15f, .85f, .15f, .15f); return; }
        if (kind == "play") { p.fillColor = p.strokeColor; p.BeginPath(); p.MoveTo(V(.18f, .08f)); p.LineTo(V(.9f, .5f)); p.LineTo(V(.18f, .92f)); p.ClosePath(); p.Fill(); return; }
        if (kind == "refresh")
        {
            float r = .36f * Mathf.Min(w, h);
            p.BeginPath(); p.Arc(V(.5f, .5f), r, 200, 340); p.Stroke(); Line(.66f, .2f, .84f, .37f, .9f, .14f);
            p.BeginPath(); p.Arc(V(.5f, .5f), r, 20, 160); p.Stroke(); Line(.34f, .8f, .16f, .63f, .1f, .86f);
            return;
        }
        if (kind == "pen") { Line(.12f, .88f, .2f, .6f, .72f, .1f, .9f, .27f, .4f, .79f, .12f, .88f); Line(.2f, .6f, .4f, .79f); return; }
        // A branching leaf motif, echoing the references' botanical corners.
        p.BeginPath(); p.MoveTo(V(.2f, .95f)); p.BezierCurveTo(V(.58f, .7f), V(.4f, .28f), V(.72f, .05f)); p.Stroke();
        for (int i = 0; i < 5; i++) { float y = .8f - i * .14f, x = .33f + i * .062f; float s = i % 2 == 0 ? -1 : 1; p.BeginPath(); p.MoveTo(V(x, y)); p.BezierCurveTo(V(x + s * .3f, y - .04f), V(x + s * .26f, y - .22f), V(x + s * .23f, y - .22f)); p.BezierCurveTo(V(x + s * .02f, y - .22f), V(x, y - .1f), V(x, y)); p.ClosePath(); p.Stroke(); }
    }
}
