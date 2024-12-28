using Godot;
using System;

public partial class DebugOverlay : CanvasLayer
{

	public DebugDraw debugDraw;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{

		debugDraw = GetNode<DebugDraw>("DebugDraw");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
