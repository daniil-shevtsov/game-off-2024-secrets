using Godot;
using System;
using System.Collections.Generic;

public partial class Gate : Node2D, Structure
{
	private Sprite2D retractedSprite;
	private Sprite2D expandedSprite;

	public HashSet<TileTrait> GetTraitsToAdd()
	{
		if (isExpanded)
		{
			return new();
		}
		else
		{
			return new HashSet<TileTrait>() { TileTrait.Wall };
		}
	}
	public HashSet<TileTrait> GetTraitsToRemove()
	{
		return new();

	}

	private bool isExpanded = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		retractedSprite = GetNode<Sprite2D>("Open");
		expandedSprite = GetNode<Sprite2D>("Closed");
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

	public bool IsExpanded()
	{
		return isExpanded;
	}
}
