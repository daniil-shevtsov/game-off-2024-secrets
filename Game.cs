using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class Game : Node2D
{
	private Player player;
	private TileMap tileMap;
	private Marker2D respawnPoint;
	private Ui ui;
	private ColorRect tileHighlight;
	private SubViewportContainer subViewportContainer;
	private SubViewport subViewport;
	private DebugDraw debugDraw;
	private DebugDraw viewportDebugDraw;

	public const int tileSize = 16;
	private Dictionary<String, Vector2> inputs = new() {
		{"left", Vector2.Left},
		{"up", Vector2.Up},
		{"down", Vector2.Down},
		{"right", Vector2.Right},
	};

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		player = (Player)FindChild("Player");
		tileMap = (TileMap)FindChild("TileMap");
		respawnPoint = (Marker2D)FindChild("RespawnPoint");
		ui = (Ui)FindChild("Ui");
		tileHighlight = (ColorRect)FindChild("TileHighlight");
		subViewportContainer = (SubViewportContainer)FindChild("SubViewportContainer");
		subViewport = (SubViewport)FindChild("SubViewport");
		debugDraw = ((DebugOverlay)FindChild("DebugOverlay")).debugDraw;
		viewportDebugDraw = ((DebugOverlay)FindChild("ViewportDebugOverlay")).debugDraw;

		respawnPoint.GlobalPosition = respawnPoint.GlobalPosition.Snapped(Vector2.One * tileSize);
		respawnPoint.GlobalPosition += Vector2.One * tileSize / 2;

		player.GlobalPosition = respawnPoint.GlobalPosition;
		player.GlobalPosition = player.GlobalPosition.Snapped(Vector2.One * tileSize);
		player.GlobalPosition += Vector2.One * tileSize / 2;
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
		return subViewport.GetViewport().GetScreenTransform().AffineInverse() * GetGlobalTransformWithCanvas() * position;
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
			var mousePosition = subViewport.GetMousePosition();
			var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
			if (hoveredTile != null)
			{
				var tileGlobalPosition = tileMap.ToGlobal(tileMap.MapToLocal(hoveredTile));

				var tileWorldCoordinates = TileMapLocalToWorld(hoveredTile);
				var hoverPosition = WorldToViewportLocal(tileWorldCoordinates);
				var logical = ViewportLocalToWorld(mousePosition);

				debugDraw.UpdateVectorToDraw("Vector2.Zero1", eventMouseMotion.Position, Vector2.Zero, Color.FromHtml("#FF0000"));
				debugDraw.UpdateVectorToDraw("Vector2.Middle1", eventMouseMotion.Position, new Vector2(1600f / 2f, 960f / 2f), Color.FromHtml("#FF0000"));
				debugDraw.UpdateVectorToDraw("Vector2.End1", eventMouseMotion.Position, new Vector2(1600f, 960f), Color.FromHtml("#FF0000"));
				debugDraw.UpdateVectorToDraw("Vector2.Zero2", eventMouseMotion.Position, ViewportLocalToWorld(WorldToViewportLocal(Vector2.Zero)), Color.FromHtml("#00FF00"));
				debugDraw.UpdateVectorToDraw("Vector2.Middle2", eventMouseMotion.Position, ViewportLocalToWorld(WorldToViewportLocal(new Vector2(400f / 2f, 240f / 2f))), Color.FromHtml("#00FF00"));
				debugDraw.UpdateVectorToDraw("Vector2.End2", eventMouseMotion.Position, ViewportLocalToWorld(WorldToViewportLocal(new Vector2(400f, 240f))), Color.FromHtml("#00FF00"));

				viewportDebugDraw.UpdateVectorToDraw("Viewport debug1", subViewport.GetMousePosition(), Vector2.Zero, Color.FromHtml("FFFF00"));
				viewportDebugDraw.UpdateVectorToDraw("Viewport debug2", subViewport.GetMousePosition(), new Vector2(400 / 2f, 240 / 2f), Color.FromHtml("FFFF00"));
				viewportDebugDraw.UpdateVectorToDraw("Viewport debug3", subViewport.GetMousePosition(), new Vector2(400, 240), Color.FromHtml("FFFF00"));

				// debugDraw.UpdateVectorToDraw("mouse difference1", eventMouseMotion.Position, tileWorldCoordinates, Color.FromHtml("#FF0000"));
				// debugDraw.UpdateVectorToDraw("mouse difference2", eventMouseMotion.Position, ViewportLocalToWorld(hoverPosition), Color.FromHtml("#0000FF"));
				// debugDraw.UpdateVectorToDraw("mouse difference3", eventMouseMotion.Position, logical, Color.FromHtml("#00FF00"));
				GD.Print($"Difference: {ViewportLocalToWorld(hoverPosition) - eventMouseMotion.Position} {subViewport.GetMousePosition() - eventMouseMotion.Position}");
				tileHighlight.GlobalPosition = WorldToViewportLocal(Vector2.Zero);
				// debugDraw.UpdateVectorToDraw("mouse difference3", eventMouseMotion.Position, tileHighlight.GlobalPosition, Color.FromHtml("#00FF00"));

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

			if (hoveredTile != null && !ui.isContextMenuShown)
			{
				ui.ShowContextMenu(
					tileMap.MapToLocal(hoveredTile),
					new() { ContextMenuAction.Copy, ContextMenuAction.Paste },
					(action) => OnContextMenuActionSelected(hoveredTile, action)
					);
			}
			else
			{
				ui.HideContextMenu();
			}
		}
	}

	private void OnContextMenuActionSelected(Vector2I hoveredTile, ContextMenuAction action)
	{

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

		GD.Print($"current={getTileType(currentTile)} potential={getTileType(potentialTile)} final={finalTileType}");

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
