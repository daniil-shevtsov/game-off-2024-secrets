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

		// respawnPoint.Position = respawnPoint.Position.Snapped(Vector2.One * tileSize);
		// respawnPoint.Position += Vector2.One * tileSize / 2;

		player.Position = respawnPoint.Position;
		// player.Position = player.Position.Snapped(Vector2.One * tileSize);
		// player.Position += Vector2.One * tileSize / 2;
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
			var globalMousePosition = GetGlobalMousePosition();
			var localMousePosition = WorldToViewportLocal(globalMousePosition);
			var magicOffset = subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
			var mousePosition = localMousePosition + magicOffset;
			var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
			if (hoveredTile != null)
			{
				var tileGlobalPosition = tileMap.ToGlobal(tileMap.MapToLocal(hoveredTile));

				var tileWorldCoordinates = TileMapLocalToWorld(hoveredTile);
				var hoverPosition = WorldToViewportLocal(tileWorldCoordinates);
				var logical = ViewportLocalToWorld(mousePosition);

				// debugDraw.UpdateVectorToDraw("Vector2.Zero1", eventMouseMotion.Position, Vector2.Zero, Color.FromHtml("#FF0000"));
				// debugDraw.UpdateVectorToDraw("Vector2.Middle1", eventMouseMotion.Position, new Vector2(1600f / 2f, 960f / 2f), Color.FromHtml("#FF0000"));
				// debugDraw.UpdateVectorToDraw("Vector2.End1", eventMouseMotion.Position, new Vector2(1600f, 960f), Color.FromHtml("#FF0000"));

				// viewportDebugDraw.UpdateVectorToDraw("Viewport debug1", mousePosition, Vector2.Zero, Color.FromHtml("FFFF00"));
				// viewportDebugDraw.UpdateVectorToDraw("Viewport debug2", mousePosition, new Vector2(400 / 2f, 240 / 2f), Color.FromHtml("FFFF00"));
				// viewportDebugDraw.UpdateVectorToDraw("Viewport debug3", mousePosition, new Vector2(400, 240), Color.FromHtml("FFFF00"));

				// viewportDebugDraw.UpdateVectorToDraw("Viewport debug4", subViewport.GetMousePosition(), Vector2.Zero, Color.FromHtml("FFFF00"));
				// viewportDebugDraw.UpdateVectorToDraw("Viewport debug5", subViewport.GetMousePosition(), WorldToViewportLocal(new Vector2(1600f / 2f, 960f / 2f)), Color.FromHtml("FF00FF"));

				var newHighlightPosition = WorldToViewportLocal(tileWorldCoordinates);

				var snapped = mousePosition.Snapped(Vector2.One * tileSize);
				var final = snapped + Vector2.One * tileSize / 2;
				var final2 = (mousePosition - Vector2.One * tileSize / 2).Snapped(Vector2.One * tileSize);
				tileHighlight.Position = final2;
				spriteHighlight.Position = final;

				GD.Print($"sub_center_position={subViewport.GetCamera2D().GetScreenCenterPosition()} global_center_position={camera.GetScreenCenterPosition()}");
				// debugDraw.UpdateVectorToDraw("to highlight local", ViewportLocalToWorld(mousePosition), ViewportLocalToWorld(snapped), Color.FromHtml("0000FF"));
				// debugDraw.UpdateVectorToDraw("to highlight local final", ViewportLocalToWorld(mousePosition), ViewportLocalToWorld(final), Color.FromHtml("FF00FF"));
				// debugDraw.UpdateVectorToDraw("to highlight local player", ViewportLocalToWorld(mousePosition), ViewportLocalToWorld(player.Position), Color.FromHtml("FF0000"));
				// debugDraw.UpdateVectorToDraw("to highlight local position", ViewportLocalToWorld(mousePosition), ViewportLocalToWorld(tileHighlight.Position), Color.FromHtml("000000"));

				// viewportDebugDraw.UpdateVectorToDraw("13213123213", WorldToViewportLocal(eventMouseMotion.Position), WorldToViewportLocal(new Vector2(1600f / 2f, 960f / 2f)), Color.FromHtml("#FFFFFF"));

				viewportDebugDraw.UpdateVectorToDraw("zero-to-mouse local", Vector2.Zero, localMousePosition, Color.FromHtml("#FF0000"));
				debugDraw.UpdateVectorToDraw("zero-to-mouse global", Vector2.Zero, globalMousePosition, Color.FromHtml("#FFFF00"));

				viewportDebugDraw.UpdateVectorToDraw("zero-to-highlight local", Vector2.Zero, tileHighlight.Position, Color.FromHtml("#0000FF"));
				debugDraw.UpdateVectorToDraw("zero-to-highlight global", Vector2.Zero, ViewportLocalToWorld(tileHighlight.Position), Color.FromHtml("#00FFFF"));

				// GD.Print($"Local: mouse={mousePosition} player={player.Position} highlight before={snapped} highlight final={final} final2={final2}");
				// tileHighlight.GlobalPosition = ViewportLocalToWorld(tileHighlight.GlobalPosition);

				// tileHighlight.Position = WorldToViewportLocal(ViewportLocalToWorld(player.Position - Vector2.One * 32f / 2f));

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
					position,
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
