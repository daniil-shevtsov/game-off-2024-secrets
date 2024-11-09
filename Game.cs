using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node2D
{
	private Player player;
	private TileMap tileMap;

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
		tileMap = (TileMap)FindChild("TileMap");

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
		var potentialNewPosition = player.GlobalPosition + potentialMove;
		player.rayCast.TargetPosition = potentialMove;
		player.rayCast.ForceRaycastUpdate();

		var currentPosition = player.GlobalPosition;
		var finalPosition = currentPosition;
		if (!player.rayCast.IsColliding())
		{
			finalPosition = potentialNewPosition;
		}
		else
		{
			finalPosition = currentPosition;
		}
		player.GlobalPosition = finalPosition;

		var currentTile = tileMap.LocalToMap(currentPosition);
		var potentialTile = tileMap.LocalToMap(potentialNewPosition);
		var finalTile = tileMap.LocalToMap(finalPosition);
		GD.Print($"current={getTileType(currentTile)} potential={getTileType(potentialTile)} final={getTileType(finalTile)}");
	}

	private String getTileType(Vector2I tileCoords)
	{
		var data = tileMap.GetCellTileData(0, tileCoords);
		if (data != null)
		{
			return (String)data.GetCustomData("type");
		}
		else
		{
			return null;
		}
	}
}
