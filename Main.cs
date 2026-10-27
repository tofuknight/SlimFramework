using Godot;

namespace GodotGame;

public partial class Main : Node2D
{
	public override void _Ready()
	{
		GD.Print("Hello from Godot C#!");
	}
}
