using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node2D
{
	private Dictionary<TileKey, TileData> tileData = new();

	private List<Upgrade> upgrades = new();

	private List<ContextMenuAction> obtainedActions = new() { ContextMenuAction.Use, ContextMenuAction.Use, ContextMenuAction.Use, ContextMenuAction.Use };

	private List<Structure> structures = new();

	private TileKey hoveredTileKey = null;
	private TileKey contextMenuTopLeftTileKey = null;
	private bool inProcessOfDying = false;

	private void InitLogic()
	{
		InitTileData();
		InitItems();
		InitStructures();
		InitRandomStuff();

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

	private Rect2 GlobalAreaToLocal(Rect2 globalRect)
	{
		return new Rect2(
			position: GlobalToLocalWithMagicOffset(globalRect.Position),
			size: GlobalToLocalWithMagicOffset(globalRect.End) - GlobalToLocalWithMagicOffset(globalRect.Position)
		);
	}

	private Rect2 LocalAreaToGlobal(Rect2 globalRect)
	{
		return new Rect2(
			position: LocalToGlobalWithMagicOffset(globalRect.Position),
			size: LocalToGlobalWithMagicOffset(globalRect.End) - LocalToGlobalWithMagicOffset(globalRect.Position)
		);
	}

	private void UpdateLogic(double delta)
	{
		if (contextMenuTopLeftTileKey != null)
		{
			ui.MoveContextMenu(LocalToGlobalWithMagicOffset(GetPositionBy(contextMenuTopLeftTileKey) - Vector2.One * tileSize / 2));
			var contextMenuArea = ui.GetContextMenuArea();
			var localTopLeft = GlobalToLocalWithMagicOffset(contextMenuArea.Position);
			var localBottomRight = GlobalToLocalWithMagicOffset(contextMenuArea.End);
			var localSize = localBottomRight - localTopLeft;
			var topLeftTileKey = GetTileKeyByPosition(localTopLeft + Vector2.One * 2f);
			var bottomRightTileKey = GetTileKeyByPosition(localBottomRight - Vector2.One * 2f);

			var tilesUnderMenu = tileData.ToList().Where(entry =>
			{
				return entry.Key.X == bottomRightTileKey.X && entry.Key.Y >= topLeftTileKey.Y && entry.Key.Y <= bottomRightTileKey.Y;
			});
			tilesUnderMenu.ToList().ForEach(entry =>
			{
				var key = entry.Key;
				var tile = entry.Value;
				if (ui.isContextMenuShown && tile.Structure == null && localSize.Y >= tileSize)
				{
					var genericStructure = new GenericStructure();
					genericStructure.TraitsToRemoveNotActivated = new() { TileTrait.Fall };
					ModifyTile(key, tile with { Structure = genericStructure });
				}
				else if (!ui.isContextMenuShown && tile.Structure is GenericStructure)
				{
					ModifyTile(key, tile with { Structure = null });
				}
			});
		}

		var playerTile = tileMap.LocalToMap(player.GlobalPosition);
		var playerTileData = tileData[new TileKey(playerTile)];

		var playerTileTraits = GetAllTileTraits(playerTileData);
		if (!inProcessOfDying && playerTileTraits.Contains(TileTrait.Fall))
		{
			KillPlayer();
		}
	}

	private void OnMouseMovement()
	{
		var mousePosition = GlobalToLocalWithMagicOffset(GetGlobalMousePosition());
		var hoveredTile = tileMap.LocalToMap(tileMap.ToLocal(mousePosition));
		if (hoveredTile != null && !ui.isContextMenuShown)
		{
			var final2 = (mousePosition - Vector2.One * tileSize / 2).Snapped(Vector2.One * tileSize);

			hoveredTileKey = new TileKey(hoveredTile);

			tileHighlight.Position = final2;
			tileHighlight.Visible = true;
		}
		else if (!ui.isContextMenuShown)
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
		}
		else
		{
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
			// SyncSpriteToPlayer();

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
		inProcessOfDying = true;
		var previousScale = globalPlayerSprite.Scale;
		var tween = CreateTween();
		tween.TweenProperty(globalPlayerSprite, "scale", Vector2.Zero, 0.5f).SetTrans(Tween.TransitionType.Quad);
		GD.Print("STARTED");
		await ToSignal(tween, Tween.SignalName.Finished);
		GD.Print("FINISHED");
		globalPlayerSprite.Scale = previousScale;
		Respawn();
		inProcessOfDying = false;
	}

	private void Respawn()
	{
		GD.Print("RESPAWN");
		player.GlobalPosition = respawnPoint.GlobalPosition;
	}

	private void InitGlobalPlayerSpriteSize()
	{
		var playerSizeInGlobalCoordinates = LocalToGlobalWithMagicOffset(player.GlobalPosition + Vector2.One * tileSize / 2f) - LocalToGlobalWithMagicOffset(player.GlobalPosition - Vector2.One * tileSize / 2f);
		globalPlayerSprite.Scale = playerSizeInGlobalCoordinates / (Vector2.One * tileSize);
	}

	private void SyncSpriteToPlayer()
	{
		globalPlayerSprite.GlobalPosition = LocalToGlobalWithMagicOffset(player.GlobalPosition);
	}

	private void InitNodeReferences()
	{
		player = (Player)FindChild("Player");
		globalPlayerSprite = (Sprite2D)FindChild("GlobalPlayerSprite");
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

		InitGlobalPlayerSpriteSize();
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

	private void InitRandomStuff()
	{
		InitGlobalPlayerSpriteSize();
		SyncSpriteToPlayer();
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
	private Sprite2D globalPlayerSprite;
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


public interface Structure
{
	public HashSet<TileTrait> GetTraitsToAdd();
	public HashSet<TileTrait> GetTraitsToRemove();
}

public interface Item
{

}