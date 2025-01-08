using Godot;
using System;
using System.Collections.Generic;

public partial class Lever : Node2D, Structure, Activator, Activatable
{

	public string Id { get; set; }

	public string TargetId { get; set; }

	[Export]
	public bool isActivated { get; set; }

	[Export]
	public Texture2D SpriteActivated { get; set; }


	[Export]
	public Texture2D SpriteDeactivated { get; set; }

	public Sprite2D sprite;

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
		UpdateSprite();
	}

	public void UpdateSprite()
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

	public TriggerType GetTriggerType()
	{
		return TriggerType.Use;
	}
}
