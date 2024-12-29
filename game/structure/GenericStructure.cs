using Godot;
using System;
using System.Collections.Generic;

public partial class GenericStructure : Node2D, Structure
{
	public string Id { get; set; }
	[Export]
	public bool isActivated = false;

	//TODO: Use resource path instead of sprite nodes
	[Export]
	public Texture2D SpriteActivated { get; set; }


	[Export]
	public Texture2D SpriteDeactivated { get; set; }

	public Sprite2D sprite;


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
		sprite = GetNode<Sprite2D>("Sprite2D");
		UpdateActivated();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void UpdateActivated()
	{
		if (isActivated)
		{
			sprite.Texture = SpriteActivated;
		}
		else
		{
			sprite.Texture = SpriteDeactivated;
		}
	}
}