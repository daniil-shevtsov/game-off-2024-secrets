using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node2D
{
	private Player player;

	public const int tileSize = 16;
	private Dictionary<String, Vector2> inputs = new() {
		{"left", Vector2.Left},
		{"up", Vector2.Up},
		{"down", Vector2.Down},
		{"right", Vector2.Right},
	};
	private Dictionary<String, bool> heldInputs = new() {
		{"left", false},
		{"up", false},
		{"down", false},
		{"right", false},
	};

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		player = GetNode<Player>("Player");

		player.Position = player.Position.Snapped(Vector2.One * tileSize);
		player.Position += Vector2.One * tileSize / 2;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		inputs.Keys.ToList().ForEach(direction =>
		{
			if (@event.IsActionPressed(direction))
			{
				heldInputs[direction] = true;
				Move(direction);
			}
			if (@event.IsActionReleased(direction))
			{
				heldInputs[direction] = false;
			}
		});
	}

	private void Move(string direction)
	{
		var potentialMove = inputs[direction] * tileSize;
		player.rayCast.TargetPosition = potentialMove;
		player.rayCast.ForceRaycastUpdate();
		if (!player.rayCast.IsColliding())
		{
			player.GlobalPosition += potentialMove;
		}
	}
}
