using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace StepPanel;

internal sealed class NixArt(ITextureProvider textures)
{
    public void Stretch(ImDrawListPtr draw, string asset, Vector2 min, Vector2 max, uint tint = uint.MaxValue)
    {
        var texture = textures.GetFromManifestResource(typeof(NixArt).Assembly, $"StepPanel.GameArt.{asset}");
        if (texture.TryGetWrap(out var wrap, out _))
            draw.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint);
    }

    public bool IsReady(string asset) =>
        textures.GetFromManifestResource(typeof(NixArt).Assembly, $"StepPanel.GameArt.{asset}")
            .TryGetWrap(out _, out _);

    public Vector2 Sprite(ImDrawListPtr draw, string asset, Vector2 anchor, Vector2 bounds, bool bottom = false,
        uint tint = uint.MaxValue)
    {
        var texture = textures.GetFromManifestResource(typeof(NixArt).Assembly, $"StepPanel.GameArt.{asset}");
        if (!texture.TryGetWrap(out var wrap, out _))
            return Vector2.Zero;

        var scale = System.MathF.Min(bounds.X / wrap.Width, bounds.Y / wrap.Height);
        var size = wrap.Size * scale;
        var top = anchor - new Vector2(size.X / 2, bottom ? size.Y : size.Y / 2);
        draw.AddImage(wrap.Handle, top, top + size, Vector2.Zero, Vector2.One, tint);
        return size;
    }

    public void Background(ImDrawListPtr draw, string asset, Vector2 origin, Vector2 size, uint tint = uint.MaxValue)
    {
        if (size.X <= 0 || size.Y <= 0)
            return;
        var texture = textures.GetFromManifestResource(typeof(NixArt).Assembly, $"StepPanel.GameArt.{asset}.jpg");
        if (!texture.TryGetWrap(out var wrap, out _))
            return;

        var sourceRatio = (float)wrap.Width / wrap.Height;
        var screenRatio = size.X / size.Y;
        var uv = new Vector2(System.MathF.Min(screenRatio / sourceRatio, 1), System.MathF.Min(sourceRatio / screenRatio, 1));
        var margin = (Vector2.One - uv) / 2;
        draw.AddImage(wrap.Handle, origin, origin + size, margin, Vector2.One - margin, tint);
    }
}
