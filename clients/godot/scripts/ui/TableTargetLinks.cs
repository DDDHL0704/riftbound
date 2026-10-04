using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

// These lines express a selected intent, never an inferred rule outcome.
public partial class TableTargetLinks : Control
{
    public Func<(Vector2 From, Vector2 To)[]>? Segments { get; set; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; ZIndex = 30; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        foreach (var (from, to) in Segments?.Invoke() ?? [])
        {
            var delta = to - from;
            if (delta.LengthSquared() < 4) continue;
            var points = new Vector2[25];
            var bend = (from + to) / 2 + new Vector2(0, -Math.Min(70, delta.Length() * .15f));
            for (var i = 0; i < points.Length; i++)
            { var t = i / 24f; points[i] = (1-t)*(1-t)*from + 2*(1-t)*t*bend + t*t*to; }
            DrawPolyline(points, new Color("102c49"), 7, true);
            DrawPolyline(points, new Color("78d6ed"), 3, true);
            var direction = (to - points[^2]).Normalized(); var wing = direction.Orthogonal();
            DrawColoredPolygon([to, to-direction*12+wing*5, to-direction*12-wing*5], new Color("78d6ed"));
        }
    }
}
