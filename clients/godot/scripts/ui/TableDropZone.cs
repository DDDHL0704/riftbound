using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class TableDropZone : PanelContainer
{
    public Func<Variant, bool>? CanDrop { get; set; }
    public Action<Variant>? Dropped { get; set; }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => CanDrop?.Invoke(data) == true;
    public override void _DropData(Vector2 atPosition, Variant data)
    { if (CanDrop?.Invoke(data) == true) Dropped?.Invoke(data); }
}
