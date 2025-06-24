using System.Collections.Generic;
using System.Data.Common;
using Godot;

public partial interface Structure
{
    public string Id { get; set; }
    public HashSet<TileTrait> GetTraitsToAdd();
    public HashSet<TileTrait> GetTraitsToRemove();

    public Texture2D GetTexture();
}
