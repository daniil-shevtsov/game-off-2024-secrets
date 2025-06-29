using Godot;
using System;
using System.Collections.Generic;

public partial class Laser : Node2D, Structure, Activatable
{
    public string Id { get; set; }

    [Export]
    public bool isActivated { get; set; }

    public Sprite2D sprite;

    public Marker2D marker2D;

    public HashSet<TileTrait> GetTraitsToAdd()
    {
        return new();
    }

    public HashSet<TileTrait> GetTraitsToRemove()
    {
        return new();
    }

    public Texture2D GetTexture()
    {
        return sprite.Texture;
    }

    public void ToggleActivation()
    {
        isActivated = !isActivated;
        // UpdateSprite();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        marker2D = GetNode<Marker2D>("Marker2D");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
