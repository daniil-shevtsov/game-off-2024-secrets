using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node2D
{
	private Dictionary<TileKey, TileData> tileData = new();

	private List<Upgrade> upgrades = new();

	private List<ContextMenuAction> obtainedActions = new() { ContextMenuAction.Use };

	private List<Structure> structures = new();

	private TileKey hoveredTileKey = null;
	private TileKey contextMenuTopLeftTileKey = null;

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
		var currentTile = GetTileBy(key);
		if (item == null)
		{
			((Node2D)currentTile.item).Visible = false;
		}
		ModifyTile(key, tileData[key] with { item = item });
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
		// var contextMenuArea = ui.GetContextMenuArea();
		// var localTopLeft = GlobalToLocalWithMagicOffset(contextMenuArea.Position);
		// var localBottomRight = GlobalToLocalWithMagicOffset(contextMenuArea.End);
		// var localSize = localBottomRight - localTopLeft;
		// var topLeftTileKey = GetTileKeyByPosition(localTopLeft + Vector2.One * 2f);
		// var bottomRightTileKey = GetTileKeyByPosition(localBottomRight - Vector2.One * 2f);
		// if (topLeftTileKey == bottomRightTileKey)
		// {
		// 	var tile = GetTileBy(topLeftTileKey);
		// 	if (tile != null && tile.Structure == null)
		// 	{
		// 		var genericStructure = new GenericStructure();
		// 		genericStructure.TraitsToRemoveNotActivated = new() { TileTrait.Fall };
		// 		ModifyTile(topLeftTileKey, tile with { Structure = genericStructure });
		// 	}
		// }
		if (contextMenuTopLeftTileKey != null)
		{
			var tile = GetTileBy(contextMenuTopLeftTileKey);

			ui.MoveContextMenu(LocalToGlobalWithMagicOffset(GetPositionBy(contextMenuTopLeftTileKey) - Vector2.One * tileSize / 2));

			if (ui.isContextMenuShown && tile.Structure == null)
			{
				var genericStructure = new GenericStructure();
				genericStructure.TraitsToRemoveNotActivated = new() { TileTrait.Fall };
				ModifyTile(contextMenuTopLeftTileKey, tile with { Structure = genericStructure });
			}
			else
			{
				ModifyTile(contextMenuTopLeftTileKey, tile with { Structure = null });
			}
		}

		var playerTile = tileMap.LocalToMap(player.GlobalPosition);
		var playerTileData = tileData[new TileKey(playerTile)];

		var playerTileTraits = GetAllTileTraits(playerTileData);
		if (playerTileTraits.Contains(TileTrait.Fall))
		{
			KillPlayer();
		}
	}

	private void OnMouseMovement()
	{
		var mousePosition = GlobalToLocalWithMagicOffset(GetGlobalMousePosition());
		var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
		if (hoveredTile != null)
		{
			var final2 = (mousePosition - Vector2.One * tileSize / 2).Snapped(Vector2.One * tileSize);

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
		var highlightedTileTopLeft = GetPositionBy(hoveredTileKey) - Vector2.One * tileSize / 2;
		var highlightedTileBottomRight = highlightedTileTopLeft + Vector2.One * tileSize;
		var positionToSpawnContextMenu = LocalToGlobalWithMagicOffset(highlightedTileTopLeft);

		if (!ui.isContextMenuShown && obtainedActions.Count > 0)
		{
			var tileSizeInGlobalCoordinates = LocalToGlobalWithMagicOffset(highlightedTileBottomRight) - LocalToGlobalWithMagicOffset(highlightedTileTopLeft);
			contextMenuTopLeftTileKey = GetTileKeyByPosition(highlightedTileTopLeft + Vector2.One * 2f);
			ui.ShowContextMenu(
				newPosition: positionToSpawnContextMenu,
				tileSizeInGlobalCoordinates: tileSizeInGlobalCoordinates,
				obtainedActions,
				(action) => OnContextMenuActionSelected(action)
			);

			// var contextMenuArea = ui.GetContextMenuArea();
			// var localTopLeft = GlobalToLocalWithMagicOffset(contextMenuArea.Position);
			// var localBottomRight = GlobalToLocalWithMagicOffset(contextMenuArea.End);
			// var localSize = localBottomRight - localTopLeft;
			// var topLeftTileKey = GetTileKeyByPosition(localTopLeft + Vector2.One * 2f);
			// var bottomRightTileKey = GetTileKeyByPosition(localBottomRight - Vector2.One * 2f);
			// if (topLeftTileKey == bottomRightTileKey)
			// {
			// 	var tile = GetTileBy(topLeftTileKey);
			// 	if (tile != null && tile.Structure == null)
			// 	{
			// 		var genericStructure = new GenericStructure();
			// 		genericStructure.TraitsToRemoveNotActivated = new() { TileTrait.Fall };
			// 		ModifyTile(topLeftTileKey, tile with { Structure = genericStructure });
			// 	}
			// }
		}
		else
		{
			// var contextMenuArea = ui.GetContextMenuArea();
			// var localTopLeft = GlobalToLocalWithMagicOffset(contextMenuArea.Position);
			// var localBottomRight = GlobalToLocalWithMagicOffset(contextMenuArea.End);
			// var localSize = localBottomRight - localTopLeft;
			// var topLeftTileKey = GetTileKeyByPosition(localTopLeft + Vector2.One * 2f);
			// var bottomRightTileKey = GetTileKeyByPosition(localBottomRight - Vector2.One * 2f);
			// if (topLeftTileKey == bottomRightTileKey)
			// {
			// 	var tile = GetTileBy(topLeftTileKey);
			// 	if (tile != null)
			// 	{
			// 		var genericStructure = new GenericStructure();
			// 		genericStructure.TraitsToRemoveNotActivated = new() { TileTrait.Fall };
			// 		ModifyTile(topLeftTileKey, tile with { Structure = null });
			// 	}
			// }

			ui.HideContextMenu();
		}
	}

	private void OnContextMenuActionSelected(ContextMenuAction action)
	{
		if (action == ContextMenuAction.Use && hoveredTileKey != null)
		{
			var hoveredTile = GetTileBy(hoveredTileKey);
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

		var shouldMove = IsTileWalkable(potentialNewTilePosition);

		if (shouldMove)
		{
			GD.Print("MOVE");
			var finalPosition = potentialNewPosition;
			var key = GetTileKeyByPosition(potentialNewPosition);
			var finalTile = tileData[key];

			player.GlobalPosition = finalPosition;

			if (finalTile.item != null)
			{
				OnPickup(key, finalTile.item);
			}
		}
	}

	private bool IsTileWalkable(Vector2I potentialNewTilePosition)
	{
		var potentialNewTile = tileData[new TileKey(potentialNewTilePosition)];
		var traits = GetAllTileTraits(potentialNewTile);

		var shouldMove = !traits.Contains(TileTrait.Wall);
		return shouldMove;
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
			var key = GetTileKeyByPosition(((Node2D)structure).GlobalPosition);
			var upgradeTileData = GetTileBy(key);
			ModifyTile(key, upgradeTileData with { Structure = structure });
		});
	}

	private void InitItems()
	{
		upgrades.Add((Upgrade)FindChild("Upgrade"));
		upgrades.Add((Upgrade)FindChild("Upgrade2"));
		upgrades.ForEach(upgrade =>
		{
			var key = GetTileKeyByPosition(upgrade.GlobalPosition);
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

	private TileData GetTileBy(TileKey hoveredTileKey)
	{
		return tileData[hoveredTileKey];
	}

	private TileKey GetTileKeyByPosition(Vector2 position)
	{
		return new TileKey(tileMap.LocalToMap(position));
	}

	private void ModifyTile(TileKey key, TileData newTileData)
	{
		tileData[key] = newTileData;
	}

	private Vector2 GetPositionBy(TileKey tileKey)
	{
		var hoveredTile = new Vector2I(tileKey.X, tileKey.Y);
		return tileMap.MapToLocal(hoveredTile);
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

	private Dictionary<String, Vector2> inputs = new() {
		{"left", Vector2.Left},
		{"up", Vector2.Up},
		{"down", Vector2.Down},
		{"right", Vector2.Right},
	};

	private int tileLayer = 0;

	public const int tileSize = 16;

	private Bridge bridge;
}

public record TileKey(int X, int Y)
{
	public TileKey(Vector2I tilePosition) : this(tilePosition.X, tilePosition.Y) { }
}
public record TileData(
	TileType type,
	Item item,
	Structure Structure
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