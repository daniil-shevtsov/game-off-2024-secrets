using System.Collections.Generic;
using System.Data.Common;
using Godot;

public partial class CheckpointMarker : Node2D
{
    Marker2D marker2D;

    public Area2D area2D;

    public override void _Ready()
    {
        marker2D = GetNode<Marker2D>("Marker2D");
        area2D = GetNode<Area2D>("Area2D");

        GD.Print($"1KEK1 Create {this} with area {area2D}");
    }
}
