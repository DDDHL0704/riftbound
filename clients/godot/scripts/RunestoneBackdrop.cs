using System;
using Godot;

namespace Riftbound.GodotClient;

public partial class RunestoneBackdrop : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), RunestoneTheme.BasaltBlack);
        DrawStoneSlabs(size);
        DrawTableSigil(size);
        DrawSidePanelGlow(size);
    }

    private void DrawStoneSlabs(Vector2 size)
    {
        const float tileWidth = 150f;
        const float tileHeight = 118f;
        var vertical = new Color(0.17f, 0.19f, 0.17f, 0.28f);
        var horizontal = new Color(0.22f, 0.24f, 0.2f, 0.22f);

        for (var x = 0f; x <= size.X; x += tileWidth)
        {
            var jitter = Jitter((int)x, 17, 18f);
            DrawLine(new Vector2(x + jitter, 0), new Vector2(x - jitter * 0.4f, size.Y), vertical, 1f);
        }

        for (var y = 0f; y <= size.Y; y += tileHeight)
        {
            var jitter = Jitter((int)y, 41, 14f);
            DrawLine(new Vector2(0, y + jitter), new Vector2(size.X, y - jitter * 0.35f), horizontal, 1f);
        }

        for (var i = 0; i < 34; i++)
        {
            var x = MathF.Abs(Jitter(i, 101, size.X * 0.9f)) + size.X * 0.05f;
            var y = MathF.Abs(Jitter(i, 203, size.Y * 0.88f)) + size.Y * 0.06f;
            var length = 18f + MathF.Abs(Jitter(i, 307, 28f));
            DrawLine(
                new Vector2(x, y),
                new Vector2(x + length, y + Jitter(i, 409, 12f)),
                new Color(0.36f, 0.39f, 0.33f, 0.16f),
                1f);
        }
    }

    private void DrawTableSigil(Vector2 size)
    {
        var rightEdge = MathF.Max(380f, size.X - 336f);
        var left = 18f;
        var width = MathF.Max(260f, rightEdge - left - 16f);
        var top = size.Y * 0.28f;
        var height = size.Y * 0.42f;
        var rect = new Rect2(left, top, width, height);

        DrawRect(rect, new Color(0.075f, 0.09f, 0.084f, 0.44f));
        DrawRect(rect, new Color(0.58f, 0.45f, 0.23f, 0.32f), false, 2f);

        var midY = rect.Position.Y + rect.Size.Y * 0.5f;
        DrawLine(new Vector2(rect.Position.X + 18f, midY), new Vector2(rect.End.X - 18f, midY), RunestoneTheme.RuneDim, 2f);
        DrawLine(new Vector2(rect.Position.X + rect.Size.X * 0.5f, rect.Position.Y + 14f), new Vector2(rect.Position.X + rect.Size.X * 0.5f, rect.End.Y - 14f), new Color(0.24f, 0.54f, 0.5f, 0.34f), 2f);

        for (var i = 0; i < 18; i++)
        {
            var x = rect.Position.X + 42f + i * ((rect.Size.X - 84f) / 17f);
            DrawRuneTick(new Vector2(x, midY), i);
        }
    }

    private void DrawRuneTick(Vector2 center, int index)
    {
        var glow = index % 3 == 0 ? RunestoneTheme.Brass : RunestoneTheme.Rune;
        var color = new Color(glow.R, glow.G, glow.B, 0.42f);
        DrawLine(center + new Vector2(-5, -8), center + new Vector2(5, 8), color, 1.4f);
        DrawLine(center + new Vector2(-5, 8), center + new Vector2(5, -8), color, 1.4f);
        if (index % 4 == 0)
        {
            DrawLine(center + new Vector2(-10, 0), center + new Vector2(10, 0), new Color(color.R, color.G, color.B, 0.25f), 1f);
        }
    }

    private void DrawSidePanelGlow(Vector2 size)
    {
        var x = MathF.Max(0, size.X - 336f);
        DrawRect(new Rect2(x, 0, 2f, size.Y), new Color(0.66f, 0.5f, 0.24f, 0.42f));
        DrawRect(new Rect2(x + 2f, 0, 26f, size.Y), new Color(0.28f, 0.49f, 0.44f, 0.08f));
    }

    private static float Jitter(int a, int b, float scale)
    {
        unchecked
        {
            var value = (uint)(a * 374761393 + b * 668265263);
            value = (value ^ (value >> 13)) * 1274126177;
            return ((value & 0xffff) / 65535f - 0.5f) * scale;
        }
    }
}
