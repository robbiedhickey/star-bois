using System.Numerics;
using Content.Shared._FTL.FtlPoints;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.Input;
using Robust.Shared.Input;

namespace Content.Client._FTL.FtlPoints;

public sealed class StarmapControl : Control
{
    public float Range = 1f;

    private List<Star> _stars = new List<Star>();
    private const float Ppd = 15f;
    private const float HoverRadius = 7.5f;

    [Dependency] private readonly IInputManager _inputManager = default!;

    private readonly Font _font;
    private Vector2 _mouseLocal = Vector2.Zero;

    public event Action<Star>? OnStarSelect;

    public StarmapControl()
    {
        IoCManager.InjectDependencies(this);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/B612_Mono/B612_Mono-Regular.ttf"), 8);
    }

    public void SetStars(List<Star> stars)
    {
        _stars = stars;
    }

    private Vector2 GetPositionOfStar(Vector2 position)
    {
        return Size / 2 + position * Ppd;
    }

    private Star? GetHoveredStar()
    {
        foreach (var star in _stars)
        {
            var uiPos = GetPositionOfStar(star.Position);
            if (Vector2.Distance(_mouseLocal, uiPos) <= HoverRadius)
                return star;
        }
        return null;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _mouseLocal = _inputManager.MouseScreenPosition.Position - GlobalPixelPosition;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var star = GetHoveredStar();
        if (star == null)
            return;

        if (Vector2.Distance(Vector2.Zero, star.Value.Position) >= Range)
            return;

        OnStarSelect?.Invoke(star.Value);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(new UIBox2(Vector2.Zero, Size), Color.Black);

        // Grid lines
        var lines = 10;
        for (var i = 0; i < lines; i++)
        {
            var xStep = Size.X / lines;
            var yStep = Size.X / lines;
            handle.DrawLine(new Vector2(i * xStep, 0), new Vector2(i * xStep, Size.Y), Color.DarkSlateGray);
            handle.DrawLine(new Vector2(0, i * yStep), new Vector2(Size.X, i * yStep), Color.DarkSlateGray);
        }

        // Warp range circle
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), Range * Ppd, Color.White, false);
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), Range * Ppd, new Color(47, 79, 79, 127));

        // Sensor range circle
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), (int)(Range * 1.5) * Ppd, Color.Blue, false);

        // Crosshair at hover position
        handle.DrawLine(_mouseLocal - new Vector2(10, 0), _mouseLocal + new Vector2(10, 0), Color.Yellow);
        handle.DrawLine(_mouseLocal - new Vector2(0, 10), _mouseLocal + new Vector2(0, 10), Color.Yellow);

        foreach (var star in _stars)
        {
            var uiPosition = GetPositionOfStar(star.Position);
            var hovered = Vector2.Distance(_mouseLocal, uiPosition) <= HoverRadius;
            var radius = hovered ? 10f : 5f;

            var color = Color.White;
            var name = star.Name;

            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range)
                color = Color.Red;

            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range * 1.5)
            {
                color = Color.DarkRed;
                name = Loc.GetString("ship-ftl-tag-oor");
            }

            if (star.Position == Vector2.Zero)
                color = Color.Blue;

            handle.DrawCircle(uiPosition, radius, color);

            if (hovered)
                handle.DrawString(_font, uiPosition + new Vector2(10, 0), name);
        }
    }
}
