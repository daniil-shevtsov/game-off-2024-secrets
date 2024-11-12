using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node2D
{
	private Player player;
	private TileMap tileMap;
	private Marker2D respawnPoint;
	private Ui ui;
	private ColorRect tileHighlight;
	private Sprite2D spriteHighlight;
	private SubViewportContainer subViewportContainer;
	private SubViewport subViewport;
	private DebugDraw debugDraw;
	private DebugDraw viewportDebugDraw;
	private Camera2D camera;

	public const int tileSize = 16;
	private Dictionary<String, Vector2> inputs = new() {
		{"left", Vector2.Left},
		{"up", Vector2.Up},
		{"down", Vector2.Down},
		{"right", Vector2.Right},
	};

	private Bridge bridge;

	private List<Upgrade> upgrades = new();

	private List<ContextMenuAction> obtainedActions = new() { ContextMenuAction.Use };

	private List<Node2D> objects = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		player = (Player)FindChild("Player");
		tileMap = (TileMap)FindChild("TileMap");
		respawnPoint = (Marker2D)FindChild("RespawnPoint");
		ui = (Ui)FindChild("Ui");
		tileHighlight = (ColorRect)FindChild("TileHighlight");
		spriteHighlight = (Sprite2D)FindChild("SpriteHighlight");
		subViewportContainer = (SubViewportContainer)FindChild("SubViewportContainer");
		subViewport = (SubViewport)FindChild("SubViewport");
		debugDraw = ((DebugOverlay)FindChild("DebugOverlay")).debugDraw;
		viewportDebugDraw = ((DebugOverlay)FindChild("ViewportDebugOverlay")).debugDraw;
		camera = (Camera2D)FindChild("Camera2D");

		upgrades.Add((Upgrade)FindChild("Upgrade"));
		upgrades.Add((Upgrade)FindChild("Upgrade2"));

		bridge = (Bridge)FindChild("Bridge");
		objects.Add((Lever)FindChild("Lever"));
		objects.Add(bridge);

		// respawnPoint.Position = respawnPoint.Position.Snapped(Vector2.One * tileSize);
		// respawnPoint.Position += Vector2.One * tileSize / 2;

		player.Position = respawnPoint.Position;
		player.Position = player.Position.Snapped(Vector2.One * tileSize);
		player.Position += Vector2.One * tileSize / 2;

		player.pickupArea.BodyEntered += OnPickup;
	}

	public void OnPickup(Node body)
	{
		var upgrade = body as Upgrade;
		if (upgrade != null)
		{
			obtainedActions.Add(upgrade.action);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private Vector2 TileMapLocalToWorld(Vector2I position)
	{
		return ViewportLocalToWorld(tileMap.MapToLocal(position));
	}
	private Vector2 ViewportLocalToWorld(Vector2 position)
	{
		return subViewport.GetViewport().GetScreenTransform() * GetGlobalTransformWithCanvas() * position;
	}

	private Vector2 WorldToViewportLocal(Vector2 position)
	{
		return subViewport.GetViewport().GetScreenTransform().AffineInverse() * GetGlobalTransformWithCanvas().AffineInverse() * position;
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

		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			var mousePosition = GetMouseLocalPositionWithMagicOffset();
			var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
			if (hoveredTile != null)
			{
				var tileGlobalPosition = tileMap.ToGlobal(tileMap.MapToLocal(hoveredTile));

				var tileWorldCoordinates = TileMapLocalToWorld(hoveredTile);
				var hoverPosition = WorldToViewportLocal(tileWorldCoordinates);
				var logical = ViewportLocalToWorld(mousePosition);

				var newHighlightPosition = WorldToViewportLocal(tileWorldCoordinates);

				var snapped = mousePosition.Snapped(Vector2.One * tileSize);
				var final = snapped + Vector2.One * tileSize / 2;
				var final2 = (mousePosition - Vector2.One * tileSize / 2).Snapped(Vector2.One * tileSize);

				tileHighlight.Position = final2;
				tileHighlight.Visible = true;
			}
			else
			{
				tileHighlight.Visible = false;
			}
		}

		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.IsReleased())
		{
			var position = eventMouseButton.Position;

			var hoveredTile = tileMap.LocalToMap(position);

			if (hoveredTile != null && !ui.isContextMenuShown && obtainedActions.Count > 0)
			{
				GD.Print("Show menu");
				ui.ShowContextMenu(
					position,
					obtainedActions,
					(action) => OnContextMenuActionSelected(hoveredTile, action)
					);
			}
			else
			{
				GD.Print("Hide menu");
				ui.HideContextMenu();
			}
		}
	}

	private Vector2 GetMouseLocalPositionWithMagicOffset()
	{
		var magicOffset = subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
		return WorldToViewportLocal(GetGlobalMousePosition()) + magicOffset;
	}

	private void OnContextMenuActionSelected(Vector2I hoveredTile, ContextMenuAction action)
	{
		if (action == ContextMenuAction.Use)
		{
			GD.Print($"Use clicked {objects.Count()}");
			var hoveredTileGlobalPosition = GetMouseLocalPositionWithMagicOffset();//TileMapLocalToWorld(hoveredTile);
			var selectedObject = objects.Find(o =>
			{
				var objectPosition = o.GlobalPosition;
				var difference = (objectPosition - hoveredTileGlobalPosition).Abs();
				var epsilon = tileSize / 4f;
				GD.Print($"Abs({objectPosition} - {hoveredTileGlobalPosition}) = {difference} <= {epsilon}");
				return difference <= new Vector2(epsilon, epsilon);
			});
			GD.Print($"Selected: {selectedObject}");

			if (selectedObject is Lever)
			{
				bridge.ToggleExpanded();
			}
		}
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
		var finalTileType = getTileType(finalTile);

		// GD.Print($"current={getTileType(currentTile)} potential={getTileType(potentialTile)} final={finalTileType}");

		if (finalTileType == TileType.Water)
		{
			KillPlayer();
		}
	}

	private TileType? getTileType(Vector2I tileCoords)
	{
		var data = tileMap.GetCellTileData(0, tileCoords);
		if (data != null)
		{
			return (TileType)Enum.Parse(typeof(TileType), (String)data.GetCustomData("type"), true);
		}
		else
		{
			return null;
		}
	}

	private void KillPlayer()
	{
		Respawn();
	}

	private void Respawn()
	{
		GD.Print("RESPAWN");
		player.GlobalPosition = respawnPoint.GlobalPosition;
	}
}
