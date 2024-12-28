using Godot;
using System;
using System.Collections.Generic;

public partial class DebugDraw : Control
{
	public Dictionary<string, VectorToDraw> vectorsToDraw = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void UpdateVectorToDraw(string key, Vector2 start, Vector2 end, Color? color = null)
	{
		vectorsToDraw[key] = new VectorToDraw(key, start, end, color);
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var vector in vectorsToDraw.Values)
		{
			var width = 4.0f;
			var color = vector.color;

			var start = vector.start;//camera.UnprojectPosition(vector.start);
			var end = vector.end;//camera.UnprojectPosition(vector.end);

			// GD.Print($"Drawing {vector.key} line from {start} to {end} with color {color}");
			DrawLine(start, end, color, width);
			DrawTriangle(end, start.DirectionTo(end), width * 2, color);
		}
	}


	public void DrawTriangle(Vector2 pos, Vector2 dir, float size, Color color)
	{
		var a = pos + dir * size;

		var b = pos + dir.Rotated(2 * Mathf.Pi / 3f) * size;

		var c = pos + dir.Rotated(4 * Mathf.Pi / 3f) * size;

		var points = new Vector2[] { a, b, c };

		DrawPolygon(points, new Color[] { color });
	}
}

public struct VectorToDraw
{
	public string key;
	public Vector2 start;
	public Vector2 end;
	public Color color;

	public VectorToDraw(string key, Vector2 start, Vector2 end, Color? color)
	{
		this.key = key;
		this.start = start;
		this.end = end;
		this.color = color ?? new Color(0, 1, 0);
	}

}
