using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Godot;
using System.Text.Json;

public partial class Game : Node2D
{
    private Dictionary<TileKey, TileData> tileData = new();

    private List<Upgrade> upgrades = new();

    private Dictionary<Tuple<TileKey, String>, LaserSprite> laserSprites = new();

    // private Dictionary<String, List<TileKey>> laserTiles = new();
    private Dictionary<TileKey, List<String>> tilesUnderLasers = new();

    private Dictionary<String, HashSet<TileKey>> laserBaseTiles = new();

    private HashSet<TileKey> tilesUnderContextMenu = new();

    private HashSet<ContextMenuAction> obtainedActions =
        new()
        {
            ContextMenuAction.Use,
            ContextMenuAction.Connect,
            ContextMenuAction.Cut,
            ContextMenuAction.Paste
        };

    private List<Structure> structures = new();

    private List<CheckpointMarker> checkpointMarkers = new();
    private CheckpointMarker lastCheckpointMarker = null;

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
        new Color(1f, 0.25f, 0.75f) // Rose
    };

    private void InitLogic()
    {
        InitTileData();
        InitItems();
        InitStructures();
        InitRandomStuff();

        Respawn();
    }

    private HashSet<TileTrait> GetAllTileTraits(TileKey tileKey, TileData tileData)
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
        var tileTraitsToAdd = tileData.AdditionalTraitsToAdd;

        var isTileUnderLaser = laserBaseTiles.Any(
            tilesUnderLaser => tilesUnderLaser.Value.Contains(tileKey)
        );
        if (isTileUnderLaser)
        {
            tileTraitsToAdd = tileTraitsToAdd.Add(TileTrait.Fall);
        }

        var totalTraitsToRemove = structureTraitsToRemove.Concat(tileTraitsToRemove);
        var totalTraitsToAdd = structureTraitsToAdd.Concat(tileTraitsToAdd);

        totalTraitsToAdd
            .ToList()
            .ForEach(trait =>
            {
                tileTypeTraits.Add(trait);
            });

        totalTraitsToRemove
            .ToList()
            .ForEach(trait =>
            {
                tileTypeTraits.Remove(trait);
            });

        return tileTypeTraits;
    }

    private void ModifyTileItem(TileKey key, Item item)
    {
        var currentTile = GetTileBy(key);
        if (item == null && currentTile.item != null)
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
            PickUpUpgrade(upgrade);
        }
    }

    private void PickUpUpgrade(Upgrade upgrade)
    {
        SetUpgrade(upgrade);
        SaveGame();
    }

    private void SetUpgrade(Upgrade upgrade)
    {
        obtainedActions.Add(upgrade.action);
        var tileKey = GetTileKeyByPosition(upgrade.GlobalPosition);
        ModifyTileItem(tileKey, null);
    }

    private Rect2 GlobalAreaToLocal(Rect2 globalRect)
    {
        return new Rect2(
            position: GlobalToLocalWithMagicOffset(globalRect.Position),
            size: GlobalToLocalWithMagicOffset(globalRect.End)
                - GlobalToLocalWithMagicOffset(globalRect.Position)
        );
    }

    private Rect2 LocalAreaToGlobal(Rect2 globalRect)
    {
        return new Rect2(
            position: LocalToGlobalWithMagicOffset(globalRect.Position),
            size: LocalToGlobalWithMagicOffset(globalRect.End)
                - LocalToGlobalWithMagicOffset(globalRect.Position)
        );
    }

    private TileKey GetTileByMovementDirection(Vector2 position, Vector2 direction)
    {
        var potentialMove = direction * tileSize;
        var potentialNewPosition = position + potentialMove;
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

            var potentialNewTile = GetTileByMovementDirection(
                player.GlobalPosition,
                inputDirection
            );

            ulong movementTimeout = 150;
            var shouldMove =
                IsTileWalkable(potentialNewTile) && elapsedSinceLastMove >= movementTimeout;
            if (shouldMove)
            {
                lastMovementTime = Time.GetTicksMsec();
                var finalPosition = GetPositionBy(potentialNewTile);

                movementTween?.Stop();
                movementTween = CreateTween();

                movementTween.TweenProperty(
                    player,
                    "global_position",
                    finalPosition,
                    movementTimeout / 1000f
                );
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

        var playerRange = GetRangeOfTile(playerTileKey, 1);
        var checkpointKeys = checkpointMarkers.Select(
            marker => Tuple.Create(GetTileKeyByPosition(marker.GlobalPosition), marker)
        );
        var checkpointsWithResults = checkpointMarkers.Select(marker =>
        {
            var checkpointTileKey = GetTileKeyByPosition(marker.GlobalPosition);
            return Tuple.Create(
                checkpointTileKey,
                CheckPositionInTileRange(checkpointTileKey, playerRange)
            );
        });

        var marker = checkpointMarkers.Find(
            marker =>
                CheckPositionInTileRange(GetTileKeyByPosition(marker.GlobalPosition), playerRange)
        );
        if (marker != null && lastCheckpointMarker != marker)
        {
            OnCheckpointAreaEntered(marker);
        }

        var playerTileTraits = GetAllTileTraits(playerTileKey, playerTileData);
        if (
            !inProcessOfDying
            && playerTileTraits.Contains(TileTrait.Fall)
            && !tilesUnderContextMenu.Contains(playerTileKey)
        )
        {
            KillPlayer();
        }
        else
        {
            var activator = playerTileData.Structure as Activator;
            var standingOnWalkActivator =
                activator != null && activator.GetTriggerType() == TriggerType.Walk;

            if (activatorStandingOnId != null && activator?.Id != activatorStandingOnId)
            {
                var oldActivator =
                    structures.Find(structure => structure.Id == activatorStandingOnId)
                    as Activator;
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

            var currentPlayerPosition = player.GlobalPosition;
            var screen = LocalToGlobalWithMagicOffset(currentPlayerPosition);
            var back = GlobalToLocalWithMagicOffset(screen);
            GD.Print(
                $"KEK current {currentPlayerPosition.X} toGlobal {screen.X} back toLocal {back.X}"
            );
        }
    }

    private TileKey GetTileByMovementDirection(TileKey tileKey, Vector2I direction)
    {
        return new TileKey(X: tileKey.X + direction.X, Y: tileKey.Y + direction.Y);
    }

    private bool CheckPositionInTileRange(TileKey tileKey, List<TileKey> range)
    {
        return range.Contains(tileKey);
    }

    private List<TileKey> GetRangeOfTile(TileKey tileKey, int size)
    {
        var topLeftTileKey = GetTileByMovementDirection(tileKey, Vector2I.One * -size);
        var bottomRightTileKey = GetTileByMovementDirection(tileKey, Vector2I.One * size);
        var tilesInArea = tileData
            .ToList()
            .Where(entry =>
            {
                var entryTile = new Vector2I(entry.Key.X, entry.Key.Y);
                var topLeftTile = new Vector2I(topLeftTileKey.X, topLeftTileKey.Y);
                var bottomRightTile = new Vector2I(bottomRightTileKey.X, bottomRightTileKey.Y);

                return entryTile.X >= topLeftTile.X
                    && entryTile.X <= bottomRightTile.X
                    && entryTile.Y >= topLeftTile.Y
                    && entryTile.Y <= bottomRightTile.Y;
            })
            .Select(entry => entry.Key)
            .ToList();

        return tilesInArea;
    }

    private async void HandleStructureLogic()
    {
        structures.ForEach(structure =>
        {
            if (structure is GenericStructure)
            {
                UpdateLaserLogic((GenericStructure)structure);
            }
        });
    }

    private async void HandleTileLogic() { }

    private void HandleContextMenu()
    {
        if (contextMenuTopLeftTileKey != null)
        {
            ui.MoveContextMenu(
                LocalToGlobalWithMagicOffset(
                    GetPositionBy(contextMenuTopLeftTileKey) - Vector2.One * tileSize / 2
                )
            );
            var contextMenuArea = ui.GetContextMenuArea();
            var localTopLeft = GlobalToLocalWithMagicOffset(contextMenuArea.Position);
            var localBottomRight = GlobalToLocalWithMagicOffset(contextMenuArea.End);
            var localSize = localBottomRight - localTopLeft;
            var topLeftTileKey = GetTileKeyByPosition(localTopLeft + Vector2.One * 2f);
            var bottomRightTileKey = GetTileKeyByPosition(localBottomRight - Vector2.One * 2f);

            var tilesUnderMenu = tileData
                .ToList()
                .Where(entry =>
                {
                    return entry.Key.X == bottomRightTileKey.X
                        && entry.Key.Y >= topLeftTileKey.Y
                        && entry.Key.Y <= bottomRightTileKey.Y;
                });

            tilesUnderContextMenu = tilesUnderMenu.Select(tile => tile.Key).ToHashSet();
        }
        else
        {
            tilesUnderContextMenu = new();
        }
    }

    private void UpdateLogic(double delta)
    {
        GD.Print($"KEK {DisplayServer.WindowGetSize().X} {DisplayServer.WindowGetSize().Y}");

        HandleContextMenu();

        HandlePlayerLogic();

        HandleStructureLogic();

        HandleTileLogic();
    }

    private void OnMouseMovement()
    {
        var globalMousePosition = GetGlobalMousePosition();
        var mousePosition = GlobalToLocalWithMagicOffset(globalMousePosition);
        var localMousePosition = tileMap.ToLocal(mousePosition);
        GD.Print(
            $"1KEK1 mousePosition global {globalMousePosition} global with offset {mousePosition} toleMap.toLocal {localMousePosition}"
        );
        var hoveredTile = tileMap.LocalToMap(localMousePosition);
        if (hoveredTile != null && !ui.isContextMenuShown)
        {
            var final2 = (mousePosition - Vector2.One * tileSize / 2).Snapped(
                Vector2.One * tileSize
            );

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
            var tileSizeInGlobalCoordinates =
                LocalToGlobalWithMagicOffset(highlightedTileBottomRight)
                - LocalToGlobalWithMagicOffset(highlightedTileTopLeft);
            contextMenuTopLeftTileKey = GetTileKeyByPosition(
                highlightedTileTopLeft + Vector2.One * 2f
            );
            ui.ShowContextMenu(
                newPosition: positionToSpawnContextMenu,
                tileSizeInGlobalCoordinates: tileSizeInGlobalCoordinates,
                obtainedActions,
                (action) => OnContextMenuActionSelected(action)
            );
        }
        else
        {
            contextMenuTopLeftTileKey = null;
            ui.HideContextMenu();
        }
    }

    //TODO: Activatable instead of GenericStructure in future
    private void ToggleTargetActivation(Activator activator)
    {
        var targetStructure =
            structures.Find(structure => structure.Id == activator.TargetId) as Activatable;
        if (targetStructure != null)
        {
            ToggleActivation(targetStructure);
            ToggleActivation(activator as Activatable);
        }
    }

    private void UpdateLaserLogic(GenericStructure structure)
    {
        if (structure != null && structure.type == StructureType.LaserBase)
        {
            var laserTileKey = GetTileKeyByPosition(structure.GlobalPosition);
            // var laserPointerKey = GetTileKeyByPosition(laser.marker2D.GlobalPosition);

            var direction = new Vector2I(0, 1);

            if (Mathf.IsEqualApprox(-90f, structure.RotationDegrees, 0.1f))
            {
                direction = new Vector2I(-1, 0);
            }

            var tilesHigherLaser = tileData
                .ToList()
                .Where(entry => entry.Key.X == laserTileKey.X && entry.Key.Y < laserTileKey.Y);

            var tilesLefterLaser = tileData
                .ToList()
                .Where(entry => entry.Key.Y == laserTileKey.Y && entry.Key.X < laserTileKey.X);
            var firstWallAheadTile = tilesHigherLaser
                .Where(
                    entry =>
                        entry.Value.type == TileType.Wall
                        || GetAllTileTraits(entry.Key, entry.Value).Contains(TileTrait.Wall)
                        || entry.Key == GetTileKeyByPosition(player.Position)
                )
                .MaxBy(entry => entry.Key.Y);
            var firstWalLefterTile = tilesLefterLaser
                .Where(
                    entry =>
                        entry.Value.type == TileType.Wall
                        || GetAllTileTraits(entry.Key, entry.Value).Contains(TileTrait.Wall)
                        || entry.Key == GetTileKeyByPosition(player.Position)
                )
                .MaxBy(entry => entry.Key.X);

            HashSet<TileKey> newLaserTiles = new();
            if (structure.isActivated)
            {
                newLaserTiles = tileData
                    .ToList()
                    .Where(entry =>
                    {
                        if (direction == new Vector2I(0, 1))
                        {
                            return entry.Key.X == laserTileKey.X
                                && entry.Key.Y > firstWallAheadTile.Key.Y
                                && entry.Key.Y < laserTileKey.Y;
                        }
                        else
                        {
                            return entry.Key.Y == laserTileKey.Y
                                && entry.Key.X > firstWalLefterTile.Key.X
                                && entry.Key.X < laserTileKey.X;
                        }
                    })
                    .Select(entry => entry.Key)
                    .ToHashSet();
            }

            HashSet<TileKey> oldLaserTiles = new();
            if (laserBaseTiles.ContainsKey(structure.Id))
            {
                oldLaserTiles = laserBaseTiles[structure.Id];
            }

            var oldAndNewLaserTiles = newLaserTiles.Concat(oldLaserTiles);

            oldAndNewLaserTiles
                .ToList()
                .ForEach(tileKey =>
                {
                    RemoveLaserFromTile(tileKey, structure.Id);
                });
            newLaserTiles
                .ToList()
                .ForEach(tileKey =>
                {
                    AddLaserToTile(tileKey, structure.Id, structure.RotationDegrees);
                });
            laserBaseTiles[structure.Id] = newLaserTiles;
        }
    }

    private void AddLaserToTile(TileKey tileKey, String laserBaseId, float rotationDegrees)
    {
        if (!tilesUnderLasers.ContainsKey(tileKey))
        {
            tilesUnderLasers[tileKey] = new();
        }
        tilesUnderLasers[tileKey].Add(laserBaseId);

        var laserSprite = (LaserSprite)laserSpriteResource.Instantiate().Duplicate();
        laserSprite.Position = GetPositionBy(tileKey);
        laserSprite.RotationDegrees = rotationDegrees;
        subviewContent.AddChild(laserSprite);
        laserSprites[Tuple.Create(tileKey, laserBaseId)] = laserSprite;
    }

    private void RemoveLaserFromTile(TileKey tileKey, String laserBaseId)
    {
        if (tilesUnderLasers.ContainsKey(tileKey))
        {
            tilesUnderLasers[tileKey].Remove(laserBaseId);
        }

        var spriteKey = Tuple.Create(tileKey, laserBaseId);
        if (laserSprites.ContainsKey(spriteKey))
        {
            laserSprites[spriteKey].Free();
            laserSprites.Remove(spriteKey);
        }
    }

    private void ToggleActivation(Activatable activatable)
    {
        activatable.ToggleActivation();

        if (activatable is not GenericStructure)
        {
            return;
        }

        var structure = (GenericStructure)activatable;
        UpdateLaserLogic(structure);
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

            var currentRememberedActivatorToConnect = activatorToConnectId;
            var isActivatorCurrentlyRemembered = currentRememberedActivatorToConnect != null;
            var activatorToConnect = selectedStructure as Activator;
            if (activatorToConnect != null && !isActivatorCurrentlyRemembered)
            {
                RememberActivatorToConnect(activatorToConnect);
            }
            else if (isActivatorCurrentlyRemembered)
            {
                ConnectActivatorToStructure(
                    currentRememberedActivatorToConnect,
                    selectedStructure.Id
                );
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

                Texture2D cutStructureTexture = selectedStructure.GetTexture();

                if (cutStructureTexture != null)
                {
                    ui.UpdateClipboardItem(cutStructureTexture);
                }
            }
        }
        else if (
            action == ContextMenuAction.Paste
            && hoveredTileKey != null
            && clipboardStructureId != null
        )
        {
            var hoveredTile = GetTileBy(hoveredTileKey);
            var clipboardStructure = structures.Find(
                structure => structure.Id == clipboardStructureId
            );
            if (clipboardStructure != null)
            {
                ModifyTile(hoveredTileKey, hoveredTile with { Structure = clipboardStructure });
                (clipboardStructure as Node2D).GlobalPosition = GetPositionBy(hoveredTileKey);
                ui.UpdateClipboardItem(null);
            }
            else
            {
                GD.PrintErr(
                    $"Could not find structure by id {clipboardStructureId} in list of cound {structures.Count}"
                );
            }
        }
    }

    private void ConnectActivatorToStructure(string activatorId, string structureId)
    {
        var activator = structures.Find(structure => structure.Id == activatorId) as Activator;
        activator.TargetId = structureId;
    }

    private void SaveGame()
    {
        var saveData = new SaveData();
        if (lastCheckpointMarker != null)
        {
            saveData.LastCheckpointName = lastCheckpointMarker.Name;
        }
        saveData.ObtainedUpgradeContextMenuActions = upgrades.Select(upgrade => upgrade.action);

        using var saveGameFile = FileAccess.Open(saveFilePath, FileAccess.ModeFlags.Write);

        var jsonString = JsonSerializer.Serialize(saveData);
        saveGameFile.StoreLine(jsonString);
    }

    private void LoadGame()
    {
        if (!FileAccess.FileExists(saveFilePath))
        {
            GD.PrintErr("Can't load save game because save does not exist");
            return;
        }

        using var saveGameFile = FileAccess.Open(saveFilePath, FileAccess.ModeFlags.Read);
        var jsonString = saveGameFile.GetLine();
        SaveData parsedSaveData = null;
        try
        {
            parsedSaveData = JsonSerializer.Deserialize<SaveData>(jsonString);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"JSON Parse Error: {exception}");
            return;
        }

        if (parsedSaveData.LastCheckpointName != null)
        {
            var lastCheckpoint = checkpointMarkers.Find(
                marker => marker.Name == parsedSaveData.LastCheckpointName
            );
            if (lastCheckpoint != null)
            {
                UpdateLastCheckpointMarker(lastCheckpoint);
                Respawn();
            }
        }
        if (parsedSaveData.ObtainedUpgradeContextMenuActions != null)
        {
            parsedSaveData.ObtainedUpgradeContextMenuActions
                .ToList()
                .ForEach(action =>
                {
                    var upgrade = upgrades.Find(upgrade => upgrade.action == action);

                    if (upgrade != null)
                    {
                        SetUpgrade(upgrade);
                    }
                });
        }
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
                var targetStructure = structures.Find(
                    structure => structure.Id == activator.TargetId
                );
                if (targetStructure != null)
                {
                    var a = LocalToGlobalWithMagicOffset(((Node2D)activator).GlobalPosition);
                    var b = LocalToGlobalWithMagicOffset(((Node2D)targetStructure).GlobalPosition);

                    var dashIndex = activator.Id.IndexOf('-');
                    var numberText = activator.Id.Substring(dashIndex + 1);
                    var parsedNumber = int.Parse(numberText);

                    var activatorNumber = structures.Count / (parsedNumber + 1);
                    var color = debugColors[parsedNumber % debugColors.Length];

                    debugDraw.UpdateVectorToDraw($"{activator.Id}", a, b, color);
                }
            }
        });
    }

    private bool IsTileWalkable(TileKey tileKey)
    {
        var potentialNewTile = tileData[tileKey];
        var traits = GetAllTileTraits(tileKey, potentialNewTile);

        var shouldMove = !traits.Contains(TileTrait.Wall);

        return shouldMove;
    }

    private async void KillPlayer()
    {
        inProcessOfDying = true;
        var previousScale = globalPlayerSprite.Scale;
        var tween = CreateTween();
        tween
            .TweenProperty(globalPlayerSprite, "scale", Vector2.Zero, 0.5f)
            .SetTrans(Tween.TransitionType.Quad);
        await ToSignal(tween, Tween.SignalName.Finished);
        globalPlayerSprite.Scale = previousScale;
        Respawn();
        inProcessOfDying = false;
    }

    private void OnCheckpointAreaEntered(CheckpointMarker marker)
    {
        UpdateLastCheckpointMarker(marker);
        SaveGame();
    }

    private void UpdateLastCheckpointMarker(CheckpointMarker marker)
    {
        lastCheckpointMarker = marker;
    }

    private void Respawn()
    {
        if (lastCheckpointMarker != null)
        {
            player.GlobalPosition = lastCheckpointMarker.GlobalPosition;
        }
        else
        {
            player.GlobalPosition = respawnPoint.GlobalPosition;
        }
    }

    private void InitGlobalPlayerSpriteSize()
    {
        var playerSizeInGlobalCoordinates =
            LocalToGlobalWithMagicOffset(player.GlobalPosition + Vector2.One * tileSize / 2f)
            - LocalToGlobalWithMagicOffset(player.GlobalPosition - Vector2.One * tileSize / 2f);
        globalPlayerSprite.Scale = playerSizeInGlobalCoordinates / (Vector2.One * tileSize);
    }

    private void SyncSpriteToPlayer()
    {
        globalPlayerSprite.GlobalPosition = LocalToGlobalWithMagicOffset(player.GlobalPosition);
    }

    private void InitNodeReferences()
    {
        player = (Player)FindChild("Player");
        player.SetCollisionLayerValue(playerCollisionLevel, true);
        player.pickupArea.SetCollisionMaskValue(playerPickupCollisionLevel, true);

        globalPlayerSprite = (Sprite2D)FindChild("GlobalPlayerSprite");
        tileMap = (TileMap)FindChild("TileMap");
        respawnPoint = (Marker2D)FindChild("RespawnPoint");
        ui = (Ui)FindChild("Ui");
        tileHighlight = (ColorRect)FindChild("TileHighlight");
        spriteHighlight = (Sprite2D)FindChild("SpriteHighlight");
        subViewportContainer = (SubViewportContainer)FindChild("SubViewportContainer");
        subViewport = (SubViewport)FindChild("SubViewport");
        subviewContent = (Node2D)FindChild("SubviewContent");
        debugDraw = ((DebugOverlay)FindChild("DebugOverlay")).debugDraw;
        viewportDebugDraw = ((DebugOverlay)FindChild("ViewportDebugOverlay")).debugDraw;
        camera = (Camera2D)FindChild("Camera2D");
        timer = GetNode<Godot.Timer>("Timer");

        laserSpriteResource = GD.Load<PackedScene>("res://game/structure/laser_sprite.tscn");

        InitGlobalPlayerSpriteSize();
    }

    private void InitTileData()
    {
        var allCoords = tileMap.GetUsedCells(tileLayer);
        allCoords
            .ToList()
            .ForEach(tileIndices =>
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
                        AdditionalTraitsToRemove: ImmutableList.Create<TileTrait>(),
                        AdditionalTraitsToAdd: ImmutableList.Create<TileTrait>()
                    );
                    tileData.Add(key, data);
                }
            });
    }

    private void InitStructures()
    {
        structures = subViewport
            .GetNode<Node2D>("SubviewContent")
            .GetChildren()
            .Where(node => node is Structure)
            .Select(structure => structure as Structure)
            .ToList();

        var index = 0;
        structures.ForEach(structure =>
        {
            structure.Id = $"{structure.GetType().Name}-{index++}";
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
                    .MinBy(
                        structure => ((Node2D)activator).GlobalPosition.DistanceTo(structure.Value)
                    )
                    .Key;
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
        InitSaveSystem();
    }

    private void InitSaveSystem()
    {
        checkpointMarkers = subviewContent
            .GetChildren()
            .Where(node => node is CheckpointMarker)
            .Select(structure => structure as CheckpointMarker)
            .ToList();

        checkpointMarkers.ForEach(marker =>
        {
            marker.area2D.SetCollisionMaskValue(playerCollisionLevel, true);
            marker.area2D.Monitoring = false;
            marker.area2D.BodyEntered += (body) =>
            {
                if (body.GetParent() is Player)
                {
                    OnCheckpointAreaEntered(marker);
                }
            };
            marker.area2D.Monitoring = true;
        });
    }

    private void InitItems()
    {
        upgrades = subviewContent
            .GetChildren()
            .Where(node => node is Upgrade)
            .Select(node =>
            {
                var upgrade = node as Upgrade;
                InitUpgrade(upgrade);
                return upgrade;
            })
            .ToList();
    }

    private void InitUpgrade(Upgrade upgrade)
    {
        upgrade.SetCollisionLayerValue(playerPickupCollisionLevel, true);

        var key = GetTileKeyByPosition(upgrade.GlobalPosition);
        ModifyTileItem(key, upgrade);
    }

    public override void _Ready()
    {
        InitNodeReferences();
        InitLogic();
        LoadGame();
    }

    public override void _Process(double delta)
    {
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
        return subViewport.GetViewport().GetScreenTransform()
            * GetGlobalTransformWithCanvas()
            * position;
    }

    private Vector2 WorldToViewportLocal(Vector2 position)
    {
        return subViewport.GetViewport().GetScreenTransform().AffineInverse()
            * GetGlobalTransformWithCanvas().AffineInverse()
            * position;
    }

    private Vector2 GetMagicalOffset()
    {
        return Vector2.Zero;
        var originalSize = new Vector2(1600, 960);
        var newSize = new Vector2(DisplayServer.WindowGetSize().X, DisplayServer.WindowGetSize().Y);
        var a = originalSize / newSize;
        var b = newSize / originalSize;

        var originalMagicalOffset = new Vector2(200, 120);

        GD.Print(
            $"KEK original size = {originalSize.X} original offset = {originalMagicalOffset.X} new size = {newSize.X}"
        );

        // return originalMagicalOffset;
    }

    // It seems magic offsets required only when mouse position is somehow involved
    private Vector2 GlobalToLocalWithMagicOffset(Vector2 position)
    {
        var screenCenterProjection = subViewport.GetCamera2D().GetScreenCenterPosition();
        var getMagicalOffset = GetMagicalOffset();
        var magicOffset = screenCenterProjection - getMagicalOffset;
        var without = WorldToViewportLocal(position);
        var with = without + magicOffset;
        GD.Print(
            $"1KEK1 position {position} screenCenter {screenCenterProjection.X} getMagicalOffset {getMagicalOffset.X} magicOffset {magicOffset.X} without {without.X} {with.X}"
        );
        return with;
    }

    private Vector2 LocalToGlobalWithMagicOffset(Vector2 localPosition)
    {
        var localWithMagicOffset = localPosition - CalculateMagicOffset();
        var result = ViewportLocalToWorld(localWithMagicOffset);
        if (localPosition == player.GlobalPosition)
        {
            GD.Print(
                $"KEK localPosition {localPosition.X} getMagicalOffset {CalculateMagicOffset().X} localWithMagicOffset {localWithMagicOffset.X} result {result.X}"
            );
        }
        return result;
    }

    private Vector2 CalculateMagicOffset()
    {
        return subViewport.GetCamera2D().GetScreenCenterPosition() - GetMagicalOffset();
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

    public class SaveData
    {
        public string LastCheckpointName { get; set; }
        public IEnumerable<ContextMenuAction> ObtainedUpgradeContextMenuActions { get; set; }
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
    private Node2D subviewContent;
    private DebugDraw debugDraw;
    private DebugDraw viewportDebugDraw;
    private Camera2D camera;

    private Godot.Timer timer;

    private PackedScene laserSpriteResource = null;

    private int tileLayer = 0;

    public const int tileSize = 16;

    private int playerCollisionLevel = 3;
    private int playerPickupCollisionLevel = 1;

    private string saveFilePath = "user://save_game.save";
    private string saveKeyLastCheckpoint = "last_checkpoint";

    private string saveKeyUpgradeActions = "upgrade_actions";
}
