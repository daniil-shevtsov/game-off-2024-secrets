using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public partial class Game : Node2D
{
	private Dictionary<TileKey, TileData> tileData = new();

	private List<Upgrade> upgrades = new();

	private List<ContextMenuAction> obtainedActions = new() {
		 ContextMenuAction.Use,
		 ContextMenuAction.Connect,
		 ContextMenuAction.Cut,
		 ContextMenuAction.Paste
	};

	private List<Structure> structures = new();

	private TileKey hoveredTileKey = null;
	private TileKey contextMenuTopLeftTileKey = null;
	private bool inProcessOfDying = false;

	private string activatorToConnectId = null;
	private string activatorStandingOnId = null;

	private string clipboardStructureId = null;

	private bool isDebugEnabled = false;

	private bool isMoving = false;

	private Tween movementTween = null;

	private ulong lastMovementTime = 0;

	private Color[] debugColors = new Color[]
		{
			new Color(1f, 0f, 0f), // Red
			new Color(0f, 1f, 0f), // Green
			new Color(0f, 0f, 1f), // Blue
			new Color(1f, 1f, 0f), // Yellow
			new Color(0f, 1f, 1f), // Cyan
			new Color(1f, 0f, 1f), // Magenta
			new Color(1f, 1f, 1f), // White
			new Color(1f, 0.5f, 0f), // Orange
			new Color(0.5f, 0f, 1f), // Violet
			new Color(0f, 0.5f, 1f), // Light Blue
			new Color(1f, 0f, 0.5f), // Pink
			new Color(0.25f, 1f, 0.75f), // Mint
			new Color(1f, 0.25f, 0.75f)  // Rose
        };

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


		var structureTraitsToRemove = tileData.Structure?.GetTraitsToRemove() ?? new();
		var structureTraitsToAdd = tileData.Structure?.GetTraitsToAdd() ?? new();

		var tileTraitsToRemove = tileData.AdditionalTraitsToRemove;
		List<TileTrait> tileTraitsToAdd = new();


		var totalTraitsToRemove = structureTraitsToRemove.Concat(tileTraitsToRemove);
		var totalTraitsToAdd = structureTraitsToAdd.Concat(tileTraitsToAdd);

		totalTraitsToRemove.ToList().ForEach(trait =>
		{
			tileTypeTraits.Remove(trait);
		});
		totalTraitsToAdd.ToList().ForEach(trait =>
		{
			tileTypeTraits.Add(trait);
		});

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

	private TileKey GetTileByMovementDirection(Vector2 direction)
	{
		var potentialMove = direction * tileSize;
		var potentialNewPosition = player.GlobalPosition + potentialMove;
		var potentialNewTilePosition = tileMap.LocalToMap(potentialNewPosition);
		return new TileKey(potentialNewTilePosition);
	}

	private async void HandlePlayerLogic()
	{
		// because Input.GetVector normalizes vector into 0.123123 and I want just -1 0 1
		var inputDirection = new Vector2(
			Input.GetAxis("left", "right"),
			Input.GetAxis("up", "down")
		);
		if (inputDirection == Vector2.Zero)
		{
			lastMovementTime = 0;
		}
		else
		{
			var currentMoveTime = Time.GetTicksMsec();
			var elapsedSinceLastMove = currentMoveTime - lastMovementTime;

			var potentialNewTile = GetTileByMovementDirection(inputDirection);

			ulong movementTimeout = 150;
			var shouldMove = IsTileWalkable(potentialNewTile) && elapsedSinceLastMove >= movementTimeout;
			if (shouldMove)
			{
				lastMovementTime = Time.GetTicksMsec();
				var finalPosition = GetPositionBy(potentialNewTile);

				movementTween?.Stop();
				movementTween = CreateTween();

				movementTween.TweenProperty(player, "global_position", finalPosition, movementTimeout / 1000f);
				await ToSignal(movementTween, Tween.SignalName.Finished);
			}
		}
		var playerTile = tileMap.LocalToMap(player.GlobalPosition);
		var playerTileKey = new TileKey(playerTile);
		var playerTileData = tileData[playerTileKey];

		if (playerTileData.item != null)
		{
			OnPickup(playerTileKey, playerTileData.item);
		}

		var playerTileTraits = GetAllTileTraits(playerTileData);
		if (!inProcessOfDying && playerTileTraits.Contains(TileTrait.Fall))
		{
			KillPlayer();
		}
		else
		{
			var activator = playerTileData.Structure as Activator;
			var standingOnWalkActivator = activator != null && activator.GetTriggerType() == TriggerType.Walk;

			if (activatorStandingOnId != null && activator?.Id != activatorStandingOnId)
			{
				var oldActivator = structures.Find(structure => structure.Id == activatorStandingOnId) as Activator;
				// var oldActivatorTile = GetTileBy(GetTileKeyByPosition(((Node2D)activator).GlobalPosition));
				if (oldActivator != null)
				{
					ToggleTargetActivation(oldActivator);
				}
				activatorStandingOnId = null;
			}

			if (standingOnWalkActivator)
			{
				if (activatorStandingOnId == null)
				{
					ToggleTargetActivation(activator);
					activatorStandingOnId = activator.Id;
				}
			}
		}
	}

	private void HandleContextMenu()
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
				if (ui.isContextMenuShown && tile.AdditionalTraitsToRemove.Count == 0 && localSize.Y >= tileSize)
				{
					tile.AdditionalTraitsToRemove.Add(TileTrait.Fall);
				}
				else if (!ui.isContextMenuShown && tile.AdditionalTraitsToRemove.Contains(TileTrait.Fall))
				{
					tile.AdditionalTraitsToRemove.Remove(TileTrait.Fall);
				}
			});
		}
	}

	private void UpdateLogic(double delta)
	{
		HandleContextMenu();

		HandlePlayerLogic();

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

	//TODO: Activatable instead of GenericStructure in future
	private void ToggleTargetActivation(Activator activator)
	{
		var targetStructure = structures.Find(structure => structure.Id == activator.TargetId) as Activatable;
		if (targetStructure != null)
		{
			ToggleActivation(targetStructure);
			ToggleActivation(activator as Activatable);
		}
	}

	private void ToggleActivation(Activatable structure)
	{
		structure.ToggleActivation();
	}

	private void RememberActivatorToConnect(Activator activator)
	{
		activatorToConnectId = activator.Id;
	}

	private void ForgetActivatorToConnect()
	{
		activatorToConnectId = null;
	}

	private void OnContextMenuActionSelected(ContextMenuAction action)
	{
		if (action == ContextMenuAction.Use && hoveredTileKey != null)
		{
			var hoveredTile = GetTileBy(hoveredTileKey);
			var selectedStructure = hoveredTile.Structure;

			var activator = selectedStructure as Activator;
			if (activator != null && activator.GetTriggerType() == TriggerType.Use)
			{
				ToggleTargetActivation(activator);
			}
		}
		else if (action == ContextMenuAction.Connect && hoveredTileKey != null)
		{
			var hoveredTile = GetTileBy(hoveredTileKey);
			var selectedStructure = hoveredTile.Structure;

			GD.Print($"Connect clicked when hovered over {selectedStructure.Id} at {hoveredTileKey}");

			var currentRememberedActivatorToConnect = activatorToConnectId;
			var isActivatorCurrentlyRemembered = currentRememberedActivatorToConnect != null;
			var activatorToConnect = selectedStructure as Activator;
			if (activatorToConnect != null && !isActivatorCurrentlyRemembered)
			{
				GD.Print($"set activator to connect from {currentRememberedActivatorToConnect} to {activatorToConnect.Id}");

				RememberActivatorToConnect(activatorToConnect);
			}
			else if (isActivatorCurrentlyRemembered)
			{
				ConnectActivatorToStructure(currentRememberedActivatorToConnect, selectedStructure.Id);
				ForgetActivatorToConnect();
			}
		}
		else if (action == ContextMenuAction.Cut && hoveredTileKey != null)
		{
			var hoveredTile = GetTileBy(hoveredTileKey);
			var selectedStructure = hoveredTile.Structure;

			if (selectedStructure != null)
			{
				clipboardStructureId = selectedStructure.Id;
				ModifyTile(hoveredTileKey, hoveredTile with { Structure = null });

				if (selectedStructure is GenericStructure)
				{
					ui.UpdateClipboardItem(((GenericStructure)selectedStructure).sprite.Texture);
				}
			}
		}
		else if (action == ContextMenuAction.Paste && hoveredTileKey != null && clipboardStructureId != null)
		{
			var hoveredTile = GetTileBy(hoveredTileKey);
			var clipboardStructure = structures.Find(structure => structure.Id == clipboardStructureId);
			if (clipboardStructure != null)
			{
				ModifyTile(hoveredTileKey, hoveredTile with { Structure = clipboardStructure });
				(clipboardStructure as Node2D).GlobalPosition = GetPositionBy(hoveredTileKey);
				ui.UpdateClipboardItem(null);
			}
			else
			{
				GD.Print($"Could not find structure by id {clipboardStructureId} in list of cound {structures.Count}");
			}

		}
	}

	private void ConnectActivatorToStructure(
		string activatorId,
		string structureId
	)
	{
		var activator = structures.Find(structure => structure.Id == activatorId) as Activator;
		activator.TargetId = structureId;
		GD.Print($"connect {activator.Id} to {structureId}");
	}

	public static Color GetColorById(int id)
	{
		// Ensure the ID wraps around the valid range of 3-bit binary combinations (0 to 7)
		int wrappedId = id % 8;

		// Decompose the wrapped ID into binary representation for R, G, B
		int r = (wrappedId & 1) > 0 ? 1 : 0; // Red bit (least significant bit)
		int g = (wrappedId & 2) > 0 ? 1 : 0; // Green bit
		int b = (wrappedId & 4) > 0 ? 1 : 0; // Blue bit

		return new Color(r, g, b);
	}

	private void DisplayActivatorConnections()
	{
		structures.ForEach(structure =>
		{
			if (structure is Activator)
			{
				var activator = structure as Activator;
				var targetStructure = structures.Find(structure => structure.Id == activator.TargetId);
				if (targetStructure != null)
				{
					var a = LocalToGlobalWithMagicOffset(((Node2D)activator).GlobalPosition);
					var b = LocalToGlobalWithMagicOffset(((Node2D)targetStructure).GlobalPosition);

					var dashIndex = activator.Id.IndexOf('-');
					var numberText = activator.Id.Substring(dashIndex + 1);
					var parsedNumber = int.Parse(numberText);

					var activatorNumber = structures.Count / (parsedNumber + 1);
					float hue = (activatorNumber * 0.6180339887f) % 1; // Golden ratio conjugate ensures uniform distribution

					var color = debugColors[parsedNumber % debugColors.Length];

					debugDraw.UpdateVectorToDraw(
											$"{activator.Id}",
											a,
											b,
											color
										);
				}
			}
		});
	}

	private bool IsTileWalkable(TileKey tileKey)
	{
		var potentialNewTile = tileData[tileKey];
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
		await ToSignal(tween, Tween.SignalName.Finished);
		globalPlayerSprite.Scale = previousScale;
		Respawn();
		inProcessOfDying = false;
	}

	private void Respawn()
	{
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
		timer = GetNode<Godot.Timer>("Timer");

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
					Structure: null,
					AdditionalTraitsToRemove: new()
					);
				tileData.Add(key, data);
			}
		});
	}

	private void InitStructures()
	{
		structures = subViewport.GetNode<Node2D>("SubviewContent").GetChildren()
		.Where(node => node is Structure)
		.Select(structure => structure as Structure)
		.ToList();
		GD.Print($"structures {structures.Count}");

		var index = 0;
		structures.ForEach(structure =>
		{
			structure.Id = $"{structure.GetType().Name}-{index++}";
			GD.Print($"{structure.Id}");
		});

		var togglableStructureDistances = structures
			.Where(structure => structure is GenericStructure)
			.ToDictionary(s => s.Id, s => (s as Node2D).GlobalPosition);

		structures
			.Select(structure => structure as Activator)
			.Where(activator => activator != null)
			.ToList()
			.ForEach(activator =>
			{
				var nearestTogglableId = togglableStructureDistances
					.MinBy(structure => ((Node2D)activator).GlobalPosition.DistanceTo(structure.Value)).Key;
				activator.TargetId = nearestTogglableId;
			});

		// set structures to all tiles
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
		GD.Print("_Process");
		UpdateLogic(delta);

		if (isDebugEnabled)
		{
			UpdateDebugDisplay(delta);
		}
	}

	private void UpdateDebugDisplay(double delta)
	{
		DisplayActivatorConnections();
	}

	public override void _UnhandledInput(InputEvent @event)
	{


		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			OnMouseMovement();
		}

		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.IsReleased())
		{
			OnMouseClick();
		}

		if (Input.IsActionJustReleased("toggle_debug_display"))
		{
			isDebugEnabled = !isDebugEnabled;
			debugDraw.Visible = isDebugEnabled;
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

	private Godot.Timer timer;

	private int tileLayer = 0;

	public const int tileSize = 16;

}
