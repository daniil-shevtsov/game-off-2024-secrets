using Godot;
using System;
using System.Collections.Generic;

public partial class Lever : Node2D, Structure
{

	public string Id { get; set; }

	public string TargetId { get; set; }

	public HashSet<TileTrait> GetTraitsToAdd()
	{
		return new();
	}
	public HashSet<TileTrait> GetTraitsToRemove()
	{
		return new();
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
