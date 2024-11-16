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

	private Dictionary<TileKey, TileData> tileData = new();

	private int tileLayer = 0;

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

	private List<Structure> structures = new();

	private TileKey hoveredTileKey = null;

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

		var allCoords = tileMap.GetUsedCells(tileLayer);
		allCoords.ToList().ForEach(tileIndices =>
		{
			var tileType = ParseTileType(tileIndices);
			if (tileType != null)
			{
				var key = new TileKey(tileIndices);

				var type = (TileType)tileType;
				var data = new TileData(
					type: type,
					Traits: new TileTraits(
						IsWalkable: type != TileType.Wall,
						isDeath: type == TileType.Water
					),
					item: null,
					Structure: null
					);
				tileData.Add(key, data);
			}
		});

		upgrades.Add((Upgrade)FindChild("Upgrade"));
		upgrades.Add((Upgrade)FindChild("Upgrade2"));
		upgrades.ForEach(upgrade =>
		{
			var key = new TileKey(tileMap.LocalToMap(upgrade.GlobalPosition));
			var upgradeTileData = tileData[key];
			ModifyTileItem(key, upgrade);
		});

		bridge = (Bridge)FindChild("Bridge");
		structures.Add((Lever)FindChild("Lever"));
		structures.Add(bridge);
		structures.ForEach(structure =>
	{
		var key = new TileKey(tileMap.LocalToMap(((Node2D)structure).GlobalPosition));
		var upgradeTileData = tileData[key];
		tileData[key] = tileData[key] with { Structure = structure };
	});

		player.Position = respawnPoint.Position;
		player.Position = player.Position.Snapped(Vector2.One * tileSize);
		player.Position += Vector2.One * tileSize / 2;

		// player.pickupArea.BodyEntered += OnPickup;
	}

	private void ModifyTileItem(TileKey key, Item item)
	{
		if (item == null)
		{
			((Node2D)tileData[key].item).Visible = false;
		}
		tileData[key] = tileData[key] with { item = item };
	}

	public void OnPickup(TileKey tileKey, Item body)
	{
		var upgrade = body as Upgrade;
		if (upgrade != null)
		{
			obtainedActions.Add(upgrade.action);
			ModifyTileItem(tileKey, null);
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

				//TODO: Set hoverkey Here
				hoveredTileKey = new TileKey(hoveredTile);

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
			if (hoveredTileKey == null)
			{
				return;
			}
			var hoveredTile = GetTileIndices(hoveredTileKey);
			var magicOffset = CalculateMagicOffset();
			var b3 = tileMap.MapToLocal(hoveredTile);
			var b2 = b3 - magicOffset;
			var b1 = ViewportLocalToWorld(b2);

			if (hoveredTile != null && !ui.isContextMenuShown && obtainedActions.Count > 0)
			{

				GD.Print("Show menu");
				ui.ShowContextMenu(
					b1,
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

	private Vector2 CalculateMagicOffset()
	{
		return subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
	}

	private Vector2 GetPositionWithMagicOffset(Vector2 position)
	{
		var magicOffset = subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
		return WorldToViewportLocal(position) + magicOffset;
	}

	private Vector2 GetMouseLocalPositionWithMagicOffset()
	{
		return GetPositionWithMagicOffset(GetGlobalMousePosition());
	}
	private void OnContextMenuActionSelected(Vector2I hoveredTilePosition, ContextMenuAction action)
	{
		if (action == ContextMenuAction.Use)
		{
			GD.Print($"Use clicked {structures.Count()}");
			var hoveredTileGlobalPosition = GetMouseLocalPositionWithMagicOffset();//TileMapLocalToWorld(hoveredTile);
			var hoveredTile = tileData[new TileKey(hoveredTilePosition)];
			var selectedStructure = hoveredTile.Structure;
			// var selectedObject = structures.Find(structure =>
			// {
			// 	var objectPosition = structure.GlobalPosition;
			// 	var difference = (objectPosition - hoveredTileGlobalPosition).Abs();
			// 	var epsilon = tileSize / 2f;
			// 	GD.Print($"Abs({objectPosition} - {hoveredTileGlobalPosition}) = {difference} <= {epsilon}");
			// 	return difference <= new Vector2(epsilon, epsilon);
			// });
			GD.Print($"Selected: {selectedStructure}");

			if (selectedStructure is Lever)
			{
				bridge.ToggleExpanded();
			}
		}
	}

	private void Move(string direction)
	{
		var potentialMove = inputs[direction] * tileSize;
		var potentialNewPosition = player.GlobalPosition + potentialMove;
		var potentialNewTilePosition = tileMap.LocalToMap(potentialNewPosition);

		var currentPosition = player.GlobalPosition;

		var shouldMove = IsTileWalkable(potentialMove, potentialNewPosition, potentialNewTilePosition);

		if (shouldMove)
		{
			var finalPosition = potentialNewPosition;
			var finalTilePosition = potentialNewTilePosition;
			var key = new TileKey(finalTilePosition);
			var finalTile = tileData[key];

			player.GlobalPosition = finalPosition;

			if (finalTile.item != null)
			{
				OnPickup(key, finalTile.item);
			}
			if (finalTile.type == TileType.Water)
			{
				KillPlayer();
			}
		}
	}

	private bool IsTileWalkable(Vector2 potentialMove, Vector2 potentialNewPosition, Vector2I potentialNewTilePosition)
	{
		player.rayCast.TargetPosition = potentialMove;
		player.rayCast.ForceRaycastUpdate();

		var potentialNewTile = tileData[new TileKey(potentialNewTilePosition)];

		var shouldMove = !player.rayCast.IsColliding();
		shouldMove = potentialNewTile.Traits.IsWalkable;

		return shouldMove;
	}

	private TileType? ParseTileType(Vector2I tileCoords)
	{
		var data = tileMap.GetCellTileData(tileLayer, tileCoords);
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

	private Vector2I GetTileIndices(TileKey tileKey)
	{
		return new Vector2I(tileKey.X, tileKey.Y);
	}
}

public record TileKey(int X, int Y)
{
	public TileKey(Vector2I tilePosition) : this(tilePosition.X, tilePosition.Y) { }
}
public record TileData(
	TileType type,
	TileTraits Traits,
	Item item,
	Structure Structure
// object content
);

public record TileTraits(
	bool IsWalkable,
	bool isDeath
);


public interface Structure
{

}

public interface Item
{

}