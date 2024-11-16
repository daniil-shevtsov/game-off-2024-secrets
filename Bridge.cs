using Godot;
using System;

public partial class Bridge : Node2D, Structure
{
	private Sprite2D retractedSprite;
	private Sprite2D expandedSprite;

	private bool isExpanded = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		retractedSprite = GetNode<Sprite2D>("Retracted");
		expandedSprite = GetNode<Sprite2D>("Expanded");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ToggleExpanded()
	{
		isExpanded = !isExpanded;
		retractedSprite.Visible = !isExpanded;
		expandedSprite.Visible = isExpanded;
	}

}
