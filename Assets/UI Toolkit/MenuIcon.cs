using UnityEngine;
using UnityEngine.UIElements;

// Small vector icons for UI Toolkit buttons. The menu fonts have no emoji or symbol glyphs, so characters like
// "▶ 🪶 ⚙ 🏠" fell back to mismatched system glyphs. These are drawn with Painter2D in the button's own text colour,
// so they stay crisp at any size and follow hover / pressed colour changes.
public class MenuIcon : VisualElement
{
    public enum Kind { Play, Journal, Gear, Home, Save, Back }

    readonly Kind kind;

    public MenuIcon(Kind kind, float size = 18f)
    {
        this.kind = kind;
        pickingMode = PickingMode.Ignore;
        style.width = size; style.height = size;
        style.flexShrink = 0;
        style.marginRight = size * .55f;
        generateVisualContent += Draw;
    }

    // Replaces a button's text with [icon][label] laid out in a centred row; the label inherits the button's font,
    // size and colour from the stylesheet.
    public static void Attach(Button button, Kind kind, string label = null)
    {
        if (button == null || button.Q<MenuIcon>() != null) return;
        string words = label ?? StripSymbols(button.text);
        button.text = string.Empty;
        button.style.flexDirection = FlexDirection.Row;
        button.style.justifyContent = Justify.Center;
        button.style.alignItems = Align.Center;
        var icon = new MenuIcon(kind);
        button.Add(icon);
        var text = new Label(words) { pickingMode = PickingMode.Ignore };
        text.style.marginLeft = 0; text.style.marginRight = 0; text.style.paddingLeft = 0; text.style.paddingRight = 0;
        button.Add(text);
        // Redraw when the button's colour changes (hover, pressed).
        button.RegisterCallback<CustomStyleResolvedEvent>(_ => icon.MarkDirtyRepaint());
        button.RegisterCallback<PointerEnterEvent>(_ => icon.MarkDirtyRepaint());
        button.RegisterCallback<PointerLeaveEvent>(_ => icon.MarkDirtyRepaint());
    }

    public static void AttachIconOnly(Button button, Kind kind, string tooltip)
    {
        if (button == null) return;
        button.tooltip = tooltip;
        button.text = string.Empty;
        if (button.Q<MenuIcon>() != null) return;

        button.style.justifyContent = Justify.Center;
        button.style.alignItems = Align.Center;
        var icon = new MenuIcon(kind, 25f);
        icon.style.marginRight = 0;
        button.Add(icon);
        button.RegisterCallback<CustomStyleResolvedEvent>(_ => icon.MarkDirtyRepaint());
        button.RegisterCallback<PointerEnterEvent>(_ => icon.MarkDirtyRepaint());
        button.RegisterCallback<PointerLeaveEvent>(_ => icon.MarkDirtyRepaint());
    }

    // Drops leading symbol / emoji characters and spacing ("▶  RESUME GAME" -> "RESUME GAME").
    static string StripSymbols(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int i = 0;
        while (i < text.Length && !char.IsLetterOrDigit(text[i])) i++;
        return text.Substring(i);
    }

    void Draw(MeshGenerationContext context)
    {
        float w = contentRect.width, h = contentRect.height;
        if (w <= 0 || h <= 0) return;
        Color color = parent != null ? parent.resolvedStyle.color : resolvedStyle.color;
        var p = context.painter2D;
        p.fillColor = color; p.strokeColor = color;
        p.lineWidth = Mathf.Max(1.6f, w * .11f);
        p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;

        switch (kind)
        {
            case Kind.Back:
                p.BeginPath();
                p.MoveTo(new Vector2(w * .72f, h * .18f));
                p.LineTo(new Vector2(w * .38f, h * .5f));
                p.LineTo(new Vector2(w * .72f, h * .82f));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(w * .4f, h * .5f));
                p.LineTo(new Vector2(w * .88f, h * .5f));
                p.Stroke();
                break;

            case Kind.Play:
                p.BeginPath();
                p.MoveTo(new Vector2(w * .26f, h * .14f));
                p.LineTo(new Vector2(w * .86f, h * .5f));
                p.LineTo(new Vector2(w * .26f, h * .86f));
                p.ClosePath();
                p.Fill();
                break;

            case Kind.Journal:
                // An open book: two pages meeting at a spine.
                p.BeginPath();
                p.MoveTo(new Vector2(w * .5f, h * .26f));
                p.LineTo(new Vector2(w * .08f, h * .16f));
                p.LineTo(new Vector2(w * .08f, h * .80f));
                p.LineTo(new Vector2(w * .5f, h * .90f));
                p.LineTo(new Vector2(w * .92f, h * .80f));
                p.LineTo(new Vector2(w * .92f, h * .16f));
                p.ClosePath();
                p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(w * .5f, h * .26f)); p.LineTo(new Vector2(w * .5f, h * .90f)); p.Stroke();
                break;

            case Kind.Gear:
            {
                Vector2 c = new Vector2(w * .5f, h * .5f);
                float outer = w * .46f, inner = w * .33f;
                p.BeginPath();
                const int teeth = 8;
                for (int t = 0; t < teeth * 2; t++)
                {
                    float a0 = (t - .32f) * Mathf.PI / teeth, a1 = (t + .32f) * Mathf.PI / teeth;
                    float r = t % 2 == 0 ? outer : inner;
                    var from = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                    var to = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                    if (t == 0) p.MoveTo(from); else p.LineTo(from);
                    p.LineTo(to);
                }
                p.ClosePath();
                p.Fill();
                // Hub hole in the button's background colour, so the gear reads as a ring.
                var parentBackground = parent != null ? parent.resolvedStyle.backgroundColor : Color.clear;
                p.fillColor = parentBackground.a > .01f ? parentBackground : Color.clear;
                p.BeginPath(); p.Arc(c, w * .14f, 0, 360); p.Fill();
                break;
            }

            case Kind.Save:
                // A bookmark ribbon: where the story was left.
                p.BeginPath();
                p.MoveTo(new Vector2(w * .24f, h * .10f));
                p.LineTo(new Vector2(w * .76f, h * .10f));
                p.LineTo(new Vector2(w * .76f, h * .90f));
                p.LineTo(new Vector2(w * .5f, h * .68f));
                p.LineTo(new Vector2(w * .24f, h * .90f));
                p.ClosePath();
                p.Stroke();
                break;

            case Kind.Home:
                p.BeginPath();
                p.MoveTo(new Vector2(w * .5f, h * .08f));
                p.LineTo(new Vector2(w * .94f, h * .48f));
                p.LineTo(new Vector2(w * .80f, h * .48f));
                p.LineTo(new Vector2(w * .80f, h * .90f));
                p.LineTo(new Vector2(w * .60f, h * .90f));
                p.LineTo(new Vector2(w * .60f, h * .64f));
                p.LineTo(new Vector2(w * .40f, h * .64f));
                p.LineTo(new Vector2(w * .40f, h * .90f));
                p.LineTo(new Vector2(w * .20f, h * .90f));
                p.LineTo(new Vector2(w * .20f, h * .48f));
                p.LineTo(new Vector2(w * .06f, h * .48f));
                p.ClosePath();
                p.Fill();
                break;
        }
    }
}
