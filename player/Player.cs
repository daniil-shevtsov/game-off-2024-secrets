using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : Area2D
{

	public RayCast2D rayCast;
	public CollisionShape2D collisionShape;
	public Area2D pickupArea;

	public override void _Ready()
	{
		rayCast = GetNode<RayCast2D>("RayCast2D");
		collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		pickupArea = GetNode<Area2D>("PickupArea");
	}

}
