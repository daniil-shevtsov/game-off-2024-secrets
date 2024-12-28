using Godot;

public enum TileType
{
    Ground,
    Water,
    Wall,
}

public partial record TileKey(int X, int Y)
{
    public TileKey(Vector2I tilePosition) : this(tilePosition.X, tilePosition.Y) { }
}
public partial record TileData(
    TileType type,
    Item item,
    Structure Structure
);

public enum TileTrait
{
    Wall,
    Fall
}