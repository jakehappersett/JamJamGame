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
	private const float PlatformCullDistanceBelowPlayer = 700f; // Remove platforms far below player
	private const float BorderThickness = 32f;
	private const float CeilingThickness = 32f;

	// Track spawned platforms
	private List<Node2D> spawnedPlatforms = new List<Node2D>();
	private float highestPlayerY = 0f;

	public override void _Ready()
	{
		platformScene = GD.Load<PackedScene>("res://Platform.tscn");
		player = GetNode<CharacterBody2D>("Player");

		// Keep the visible start point at the bottom of the background art.
		AlignBackgroundsToFloor();
		CreateBorders();

		// Create initial platforms
		SpawnInitialPlatforms();
	}

	private void AlignBackgroundsToFloor()
	{
		var floor = GetNodeOrNull<StaticBody2D>("Floor");
		if (floor == null)
		{
			return;
		}

		float floorY = floor.GlobalPosition.Y;
		AlignBackgroundLayerToFloor("back", floorY);
		AlignBackgroundLayerToFloor("mountain", floorY);
		AlignBackgroundLayerToFloor("moon", floorY);
		AlignBackgroundLayerToFloor("stars", floorY);
	}

	private void AlignBackgroundLayerToFloor(string layerName, float floorY)
	{
		var layer = GetNodeOrNull<Node2D>(layerName);
		if (layer == null)
		{
			return;
		}

		var sprite = layer.GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null || sprite.Texture == null)
		{
			return;
		}

		// Sprite2D is centered by default, so move its center up by half height.
		float textureHeight = sprite.Texture.GetHeight();
		float totalScaleY = layer.Scale.Y * sprite.Scale.Y;
		float halfHeight = textureHeight * totalScaleY * 0.5f;

		sprite.GlobalPosition = new Vector2(layer.GlobalPosition.X, floorY - halfHeight);
	}

	public override void _Process(double delta)
	{
		if (player != null)
		{
			// CullOffscreenPlatforms();

			// Update highest player position
			highestPlayerY = Mathf.Min(highestPlayerY, player.GlobalPosition.Y);

			// Check if we need to spawn new platforms
			if (ShouldSpawnPlatforms())
			{
				SpawnNewPlatforms();
			}
		}
	}

	private void CullOffscreenPlatforms()
	{
		float cullY = player.GlobalPosition.Y + PlatformCullDistanceBelowPlayer;

		// Iterate backwards so removals are safe while traversing.
		for (int i = spawnedPlatforms.Count - 1; i >= 0; i--)
		{
			Node2D platform = spawnedPlatforms[i];
			if (!IsInstanceValid(platform))
			{
				spawnedPlatforms.RemoveAt(i);
				continue;
			}

			if (platform.GlobalPosition.Y > cullY)
			{
				spawnedPlatforms.RemoveAt(i);
				platform.QueueFree();
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
		if (!TryGetBackBounds(out float left, out float right, out float top, out float bottom))
		{
			return;
		}

		float worldHeight = bottom - top;
		float sideCenterY = top + worldHeight * 0.5f;

		// Keep inside faces of side walls aligned to the visual background edges.
		float leftWallX = left - BorderThickness * 0.5f;
		float rightWallX = right + BorderThickness * 0.5f;

		CreateBorderWall("LeftWall", new Vector2(leftWallX, sideCenterY), new Vector2(BorderThickness, worldHeight));
		CreateBorderWall("RightWall", new Vector2(rightWallX, sideCenterY), new Vector2(BorderThickness, worldHeight));

	}

	private bool TryGetBackBounds(out float left, out float right, out float top, out float bottom)
	{
		left = 0f;
		right = 0f;
		top = 0f;
		bottom = 0f;

		var layer = GetNodeOrNull<Node2D>("back");
		if (layer == null)
		{
			return false;
		}

		var sprite = layer.GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null || sprite.Texture == null)
		{
			return false;
		}

		float width = sprite.Texture.GetWidth() * layer.Scale.X * sprite.Scale.X;
		float height = sprite.Texture.GetHeight() * layer.Scale.Y * sprite.Scale.Y;

		left = sprite.GlobalPosition.X - width * 0.5f;
		right = sprite.GlobalPosition.X + width * 0.5f;
		top = sprite.GlobalPosition.Y - height * 0.5f;
		bottom = sprite.GlobalPosition.Y + height * 0.5f;
		return true;
	}

	private void CreateBorderWall(string name, Vector2 position, Vector2 size)
	{
		var wall = GetNodeOrNull<StaticBody2D>(name);
		if (wall == null)
		{
			wall = new StaticBody2D();
			wall.Name = name;
			AddChild(wall);

			var collisionShape = new CollisionShape2D();
			collisionShape.Name = "CollisionShape2D";
			wall.AddChild(collisionShape);
		}

		wall.GlobalPosition = position;

		var existingCollision = wall.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (existingCollision == null)
		{
			existingCollision = new CollisionShape2D();
			existingCollision.Name = "CollisionShape2D";
			wall.AddChild(existingCollision);
		}

		var rectShape = existingCollision.Shape as RectangleShape2D;
		if (rectShape == null)
		{
			rectShape = new RectangleShape2D();
			existingCollision.Shape = rectShape;
		}

		rectShape.Size = size;
	}
}
