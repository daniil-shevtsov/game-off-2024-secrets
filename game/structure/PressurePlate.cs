using Godot;
using System;
using System.Collections.Generic;

public partial class PressurePlate : Node2D, Structure, Activator
{

	public string Id { get; set; }

	public string TargetId { get; set; }

	[Export]
	public bool isActivated = false;

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
		ToggleActivation();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ToggleActivation()
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
		return TriggerType.Walk;
	}

}
