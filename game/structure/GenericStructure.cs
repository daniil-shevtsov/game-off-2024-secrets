using Godot;
using System;
using System.Collections.Generic;

public partial class GenericStructure : Node, Structure
{
	public string Id { get; set; }
	[Export]
	public bool isActivated = false;
	[Export]
	public Sprite2D Sprite2D { get; set; }

	public HashSet<TileTrait> TraitsToAddNotActivated = new();
	public HashSet<TileTrait> TraitsToRemoveNotActivated = new();
	public HashSet<TileTrait> TraitsToAddActivated = new();
	public HashSet<TileTrait> TraitsToRemoveActivated = new();

	public HashSet<TileTrait> GetTraitsToAdd()
	{
		if (isActivated)
		{
			return TraitsToAddActivated;
		}
		else
		{
			return TraitsToAddNotActivated;
		}
	}
	public HashSet<TileTrait> GetTraitsToRemove()
	{
		if (isActivated)
		{
			return TraitsToRemoveActivated;
		}
		else
		{
			return TraitsToRemoveNotActivated;
		}

	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}