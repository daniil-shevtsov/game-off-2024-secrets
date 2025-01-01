using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public partial class Ui : CanvasLayer
{
	private VBoxContainer contextMenu;

	private Panel clipboardPanel;
	private TextureRect clipboardItem;

	public bool isContextMenuShown { get { return contextMenu.Visible; } }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		contextMenu = (VBoxContainer)FindChild("ContextMenu");
		clipboardPanel = GetNode<Panel>("ClipboardPanel");
		clipboardItem = clipboardPanel.GetNode<TextureRect>("ClipboardItem");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ShowContextMenu(
		Vector2 newPosition,
		Vector2 tileSizeInGlobalCoordinates,
		List<ContextMenuAction> actions,
		Action<ContextMenuAction> onActionSelected
	)
	{
		contextMenu.GlobalPosition = newPosition;
		contextMenu.Visible = true;

		contextMenu.GetChildren().ToList().ForEach(oldAction =>
		{
			contextMenu.RemoveChild(oldAction);
			oldAction.QueueFree();
		});
		actions.ForEach(action =>
		{
			var button = new Button();
			button.Text = action.ToString();
			button.Name = action.ToString();
			button.FocusMode = Control.FocusModeEnum.None;
			button.CustomMinimumSize = new Vector2(
				tileSizeInGlobalCoordinates.X,
				tileSizeInGlobalCoordinates.Y / 2f
			);

			contextMenu.AddChild(button);
			button.Pressed += () => { onActionSelected(action); };
		});
	}

	public void MoveContextMenu(Vector2 newPosition)
	{
		contextMenu.GlobalPosition = newPosition;
	}

	public Rect2 GetContextMenuArea()
	{
		return contextMenu.GetGlobalRect();
	}

	public void HideContextMenu()
	{
		contextMenu.Visible = false;
	}

	public void UpdateClipboardItem(Texture2D icon)
	{
		clipboardItem.Texture = icon;
	}
}
