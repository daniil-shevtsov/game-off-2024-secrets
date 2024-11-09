using Godot;
using System;

public partial class Player : CharacterBody2D
{
	public const float Speed = 300.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		Vector2 direction = Input.GetVector("left", "right", "forward", "backward");
		if (direction != Vector2.Zero)
		{
			velocity = direction * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0f, Speed);
			velocity.Y = Mathf.MoveToward(velocity.Y, 0f, Speed);

		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
