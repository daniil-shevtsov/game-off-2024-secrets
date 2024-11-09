using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : Area2D
{

	public RayCast2D rayCast;

	public override void _Ready()
	{
		rayCast = GetNode<RayCast2D>("RayCast2D");
	}

}
