using Godot;
using System;

public partial interface Activator
{
	public string Id { get; set; }
	public string TargetId { get; set; }

	public void ToggleActivation();

	public TriggerType GetTriggerType();

}
