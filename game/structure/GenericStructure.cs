using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GenericStructure : Node2D, Structure, Activatable
{
	public string Id { get; set; }

	public bool isTemporary = false;

	[Export]
	public bool isActivated { get; set; }

	//TODO: Use resource path instead of sprite nodes
	[Export]
	public Texture2D SpriteActivated { get; set; }


	[Export]
	public Texture2D SpriteDeactivated { get; set; }

	public Sprite2D sprite;

	[Export]
	public Godot.Collections.Array<TileTrait> TraitsToAddNotActivated = new();
	[Export]
	public Godot.Collections.Array<TileTrait> TraitsToRemoveNotActivated = new();
	[Export]
	public Godot.Collections.Array<TileTrait> TraitsToAddActivated = new();
	[Export]
	public Godot.Collections.Array<TileTrait> TraitsToRemoveActivated = new();

	public HashSet<TileTrait> GetTraitsToAdd()
	{
		if (isActivated)
		{
			return TraitsToAddActivated.ToHashSet();
		}
		else
		{
			return TraitsToAddNotActivated.ToHashSet();
		}
	}
	public HashSet<TileTrait> GetTraitsToRemove()
	{
		if (isActivated)
		{
			return TraitsToRemoveActivated.ToHashSet();
		}
		else
		{
			return TraitsToRemoveNotActivated.ToHashSet();
		}

	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		sprite = GetNode<Sprite2D>("Sprite2D");
		isActivated = false;
		UpdateSprite();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ToggleActivation()
	{
		isActivated = !isActivated;
		GD.Print($"Set {Id} to {isActivated}");

		UpdateSprite();
	}

	private void UpdateSprite()
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