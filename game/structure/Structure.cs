using System.Collections.Generic;

public partial interface Structure
{
    public HashSet<TileTrait> GetTraitsToAdd();
    public HashSet<TileTrait> GetTraitsToRemove();
}