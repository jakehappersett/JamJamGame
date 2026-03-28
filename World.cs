using Godot;
using System;
using System.Collections.Generic;

public partial class World : Node2D
{
	// Platform scene to instantiate
	private PackedScene platformScene;

	// Player reference
	private CharacterBody2D player;

	// Platform spawning parameters
	private const float PlatformWidth = 80f;
	private const float PlatformHeight = 20f;
	private const float SpawnDistance = 200f; // Distance above player to spawn new platforms
	private const float PlatformSpacingY = 120f; // Vertical spacing between platform rows
	private const float PlatformSpacingX = 240f; // Horizontal range for platform placement
	private const float MinHorizontalGap = 100f; // Minimum x-gap between neighboring platforms
	private const float MinVerticalGap = 90f; // Minimum y-gap to avoid crowding

	// Track spawned platforms
	private List<Node2D> spawnedPlatforms = new List<Node2D>();
	private float highestPlayerY = 0f;

	public override void _Ready()
	{
		// Load the platform scene
		platformScene = GD.Load<PackedScene>("res://Platform.tscn");

		// Get player reference
		player = GetNode<CharacterBody2D>("Player");

		// Create initial platforms
		SpawnInitialPlatforms();
	}

	public override void _Process(double delta)
	{
		if (player != null)
		{
			// Update highest player position
			highestPlayerY = Mathf.Min(highestPlayerY, player.GlobalPosition.Y);

			// Check if we need to spawn new platforms
			if (ShouldSpawnPlatforms())
			{
				SpawnNewPlatforms();
			}
		}
	}


	private void SpawnInitialPlatforms()
	{
		// Spawn a few platforms above the floor with consistent spacing and no overlaps
		for (int i = 0; i < 5; i++)
		{
			float y = 50 - (i * PlatformSpacingY);
			float x = (i - 2) * (PlatformWidth + MinHorizontalGap);

			// clamp x into range in case of platform count or width changes
			x = Mathf.Clamp(x, -PlatformSpacingX, PlatformSpacingX);

			SpawnPlatformAt(new Vector2(x, y));
		}
	}

	private bool ShouldSpawnPlatforms()
	{
		// Check if player has climbed high enough to need new platforms
		float lowestPlatformY = GetLowestPlatformY();
		return player.GlobalPosition.Y < lowestPlatformY + SpawnDistance;
	}

	private void SpawnNewPlatforms()
	{
		float lowestY = GetLowestPlatformY();
		float targetY = lowestY - PlatformSpacingY;

		// Spawn 2-3 platforms at this level
		uint numPlatforms = GD.Randi() % 3 + 2;
		for (int i = 0; i < numPlatforms; i++)
		{
			int attempts = 0;
			while (attempts < 12)
			{
				float x = (GD.Randf() - 0.5f) * PlatformSpacingX * 2;
				Vector2 candidate = new Vector2(x, targetY);

				if (SpawnPlatformAt(candidate))
				{
					break;
				}

				attempts++;
			}
		}
	}

	private bool SpawnPlatformAt(Vector2 position)
	{
		if (!IsPlatformPositionValid(position))
		{
			return false;
		}

		var platform = platformScene.Instantiate<Node2D>();
		platform.GlobalPosition = position;
		AddChild(platform);
		spawnedPlatforms.Add(platform);
		return true;
	}

	private bool IsPlatformPositionValid(Vector2 candidate)
	{
		foreach (var platform in spawnedPlatforms)
		{
			Vector2 existing = platform.GlobalPosition;
			float dy = Mathf.Abs(existing.Y - candidate.Y);
			float dx = Mathf.Abs(existing.X - candidate.X);

			// Too close vertically and horizontally
			if (dy < MinVerticalGap && dx < PlatformWidth + MinHorizontalGap)
			{
				return false;
			}

			// Overlaps in world area
			if (dy < PlatformHeight && dx < PlatformWidth)
			{
				return false;
			}
		}

		return true;
	}

	private float GetLowestPlatformY()
	{
		float lowestY = float.MaxValue;
		foreach (var platform in spawnedPlatforms)
		{
			if (platform.GlobalPosition.Y < lowestY)
			{
				lowestY = platform.GlobalPosition.Y;
			}
		}
		return lowestY;
	}

	private void CreateBorders()
	{
		// Left wall
		CreateBorderWall("LeftWall", new Vector2(-200, 0), new Vector2(50, 1000));

		// Right wall
		CreateBorderWall("RightWall", new Vector2(1000, 0), new Vector2(50, 1000));

		// Ceiling (at mountain peak)
		CreateBorderWall("Ceiling", new Vector2(0, -500), new Vector2(1500, 50));
	}

	private void CreateBorderWall(string name, Vector2 position, Vector2 size)
	{
		var wall = new StaticBody2D();
		wall.Name = name;
		wall.GlobalPosition = position;
		AddChild(wall);

		var collisionShape = new CollisionShape2D();
		var rectShape = new RectangleShape2D();
		rectShape.Size = size;
		collisionShape.Shape = rectShape;
		wall.AddChild(collisionShape);
	}
}
