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

	private void InitLogic()
	{
		InitTileData();
		InitItems();
		InitStructures();

		Respawn();
	}

	private HashSet<TileTrait> GetAllTileTraits(TileData tileData)
	{
		var tileTypeTraits = new HashSet<TileTrait>();
		if (tileData.type == TileType.Water)
		{
			tileTypeTraits.Add(TileTrait.Fall);
		}
		else if (tileData.type == TileType.Wall)
		{
			tileTypeTraits.Add(TileTrait.Wall);
		}

		if (tileData.Structure != null)
		{
			tileData.Structure.GetTraitsToRemove().ToList().ForEach(trait =>
			{
				tileTypeTraits.Remove(trait);
			});
			tileData.Structure.GetTraitsToAdd().ToList().ForEach(trait =>
			{
				tileTypeTraits.Add(trait);
			});
		}

		return tileTypeTraits;
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

	private void UpdateLogic(double delta)
	{
		var playerTile = tileMap.LocalToMap(player.GlobalPosition);
		var data = tileData[new TileKey(playerTile)];

		var traits = GetAllTileTraits(data);
		if (traits.Contains(TileTrait.Fall))
		{
			KillPlayer();
		}
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
			OnMouseMovement();
		}

		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.IsReleased())
		{
			OnMouseClick();
		}
	}

	private void OnMouseMovement()
	{
		var mousePosition = GlobalToLocalWithMagicOffset(GetGlobalMousePosition());
		var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
		if (hoveredTile != null)
		{
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

	private void OnMouseClick()
	{
		if (hoveredTileKey == null)
		{
			return;
		}
		var hoveredTile = GetTileIndices(hoveredTileKey);
		var hoveredTileLocalPosition = tileMap.MapToLocal(hoveredTile);
		var positionToSpawnContextMenu = LocalToGlobalWithMagicOffset(hoveredTileLocalPosition);

		if (!ui.isContextMenuShown && obtainedActions.Count > 0)
		{

			GD.Print("Show menu");
			ui.ShowContextMenu(
				positionToSpawnContextMenu,
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

	private void OnContextMenuActionSelected(Vector2I hoveredTilePosition, ContextMenuAction action)
	{
		if (action == ContextMenuAction.Use)
		{
			var hoveredTile = tileData[new TileKey(hoveredTilePosition)];
			var selectedStructure = hoveredTile.Structure;

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

		var shouldMove = IsTileWalkable(potentialMove, potentialNewPosition, potentialNewTilePosition);

		if (shouldMove)
		{
			GD.Print("MOVE");
			var finalPosition = potentialNewPosition;
			var finalTilePosition = potentialNewTilePosition;
			var key = new TileKey(finalTilePosition);
			var finalTile = tileData[key];

			player.GlobalPosition = finalPosition;

			if (finalTile.item != null)
			{
				OnPickup(key, finalTile.item);
			}
		}
	}

	private bool IsTileWalkable(Vector2 potentialMove, Vector2 potentialNewPosition, Vector2I potentialNewTilePosition)
	{
		var potentialNewTile = tileData[new TileKey(potentialNewTilePosition)];

		Bridge bridge = null;
		if (potentialNewTile.Structure is Bridge)
		{
			bridge = (Bridge)potentialNewTile.Structure;
		}
		var isBridgeExpanded = bridge?.IsExpanded();

		var shouldMove = potentialNewTile.Traits.IsWalkable || isBridgeExpanded == true;
		GD.Print($"Bridge: {bridge} {isBridgeExpanded} {shouldMove}");
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

	private async void KillPlayer()
	{
		var tween = CreateTween();
		tween.TweenProperty(player, "scale", Vector2.Zero, 0.5f).SetTrans(Tween.TransitionType.Quad);
		await ToSignal(tween, Tween.SignalName.Finished);
		player.Scale = Vector2.One;
		Respawn();
	}

	private void Respawn()
	{
		GD.Print("RESPAWN");
		player.GlobalPosition = respawnPoint.GlobalPosition;
	}

	private void InitNodeReferences()
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
	}

	private void InitTileData()
	{
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
	}

	private void InitStructures()
	{
		bridge = (Bridge)FindChild("Bridge");
		structures.Add((Lever)FindChild("Lever"));
		structures.Add(bridge);
		structures.ForEach(structure =>
		{
			var key = new TileKey(tileMap.LocalToMap(((Node2D)structure).GlobalPosition));
			var upgradeTileData = tileData[key];
			tileData[key] = tileData[key] with { Structure = structure };
		});
	}

	private void InitItems()
	{
		upgrades.Add((Upgrade)FindChild("Upgrade"));
		upgrades.Add((Upgrade)FindChild("Upgrade2"));
		upgrades.ForEach(upgrade =>
		{
			var key = new TileKey(tileMap.LocalToMap(upgrade.GlobalPosition));
			var upgradeTileData = tileData[key];
			ModifyTileItem(key, upgrade);
		});
	}

	public override void _Ready()
	{
		InitNodeReferences();
		InitLogic();
	}

	public override void _Process(double delta)
	{
		UpdateLogic(delta);
	}

	private Vector2I GetTileIndices(TileKey tileKey)
	{
		return new Vector2I(tileKey.X, tileKey.Y);
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

	// It seems magic offset is required only when mouse position is somehow involved
	private Vector2 GlobalToLocalWithMagicOffset(Vector2 position)
	{
		var magicOffset = subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
		return WorldToViewportLocal(position) + magicOffset;
	}

	private Vector2 LocalToGlobalWithMagicOffset(Vector2 localPosition)
	{
		var localWithMagicOffset = localPosition - CalculateMagicOffset();
		return ViewportLocalToWorld(localWithMagicOffset);
	}

	private Vector2 CalculateMagicOffset()
	{
		return subViewport.GetCamera2D().GetScreenCenterPosition() - new Vector2(200, 120);
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
);

public record TileTraits(
	bool IsWalkable,
	bool isDeath
);

public enum TileTrait
{
	Wall,
	Fall
}


public interface Structure
{
	public HashSet<TileTrait> GetTraitsToAdd();
	public HashSet<TileTrait> GetTraitsToRemove();
}

public interface Item
{

}