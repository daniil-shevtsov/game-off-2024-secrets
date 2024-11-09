using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : Area2D
{

	public const int tileSize = 16;
	private Dictionary<String, Vector2> inputs = new() {
		{"left", Vector2.Left},
		{"up", Vector2.Up},
		{"down", Vector2.Down},
		{"right", Vector2.Right},
	};

	public const float Speed = 300.0f;

	public override void _Ready()
	{
		Position = Position.Snapped(Vector2.One * tileSize);
		Position += Vector2.One * tileSize / 2;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		inputs.Keys.ToList().ForEach(direction =>
		{
			if (@event.IsActionPressed(direction))
			{
				Move(direction);
			}
		});
	}

	private void Move(string direction)
	{
		GlobalPosition += inputs[direction] * tileSize;
	}

}
