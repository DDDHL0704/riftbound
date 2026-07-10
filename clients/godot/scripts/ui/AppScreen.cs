using Godot;

namespace Riftbound.GodotClient.Ui;

public abstract partial class AppScreen : Control
{
    public virtual void SetScreenVisible(bool visible)
    {
        Visible = visible;
    }
}
