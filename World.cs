using Godot;
using System;
using System.Collections.Generic;

public partial class World : Node2D
{
	// Platform scenes to instantiate
	private PackedScene[] platformScenes;

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
	private const float WinCameraDuration = 1.2f;
	private const float WinMaxZoom = 12f;
	private const float WinMinZoom = 0.1f;
	private const float WinMinimumFrameHeight = 24f;
	private const float DebugStartDistanceBelowWin = 72f;

	[Export]
	private bool debugStartNearWin = true;

	// Track spawned platforms
	private List<Node2D> spawnedPlatforms = new List<Node2D>();
	private float highestPlayerY = 0f;
	private Camera2D playerCamera;
	private Tween winCameraTween;
	private GameState gameState = GameState.Playing;

	private enum GameState
	{
		Playing,
		WinTransition,
		Won
	}

	public override void _Ready()
	{
		platformScenes = new PackedScene[]
		{
			GD.Load<PackedScene>("res://platform_small.tscn"),
			GD.Load<PackedScene>("res://platform_med.tscn"),
			GD.Load<PackedScene>("res://Platform.tscn")
		};
		player = GetNode<CharacterBody2D>("Player");
		playerCamera = player?.GetNodeOrNull<Camera2D>("Camera2D");

		// Keep the visible start point at the bottom of the background art.
		AlignBackgroundsToFloor();
		CreateBorders();
		PositionPlayerNearWinForDebug();

		// Create initial platforms
		SpawnInitialPlatforms();
	}

	private void PositionPlayerNearWinForDebug()
	{
		if (!debugStartNearWin || player == null || !TryGetLayerBounds("back", out Rect2 backBounds))
		{
			return;
		}

		Vector2 debugStartPosition = new Vector2(backBounds.GetCenter().X, backBounds.Position.Y + DebugStartDistanceBelowWin);
		player.GlobalPosition = debugStartPosition;
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
		if (player == null || gameState != GameState.Playing)
		{
			return;
		}

		// Update highest player position
		highestPlayerY = Mathf.Min(highestPlayerY, player.GlobalPosition.Y);

		if (CheckWinCondition())
		{
			HandleWin();
			return;
		}

		// Check if we need to spawn new platforms
		if (ShouldSpawnPlatforms())
		{
			SpawnNewPlatforms();
		}
	}

	private bool CheckWinCondition()
	{
		if (!TryGetLayerBounds("back", out Rect2 backBounds))
		{
			return false;
		}

		return player.GlobalPosition.Y <= backBounds.Position.Y;
	}

	private void HandleWin()
	{
		if (gameState != GameState.Playing)
		{
			return;
		}

		gameState = GameState.WinTransition;

		if (player is Player playerNode)
		{
			playerNode.FreezeForWin();
		}

		CullAllPlatforms();
		StartWinCameraTween();
	}

	private void StartWinCameraTween()
	{
		if (playerCamera == null || !TryGetWinFocusRect(out Rect2 focusRect))
		{
			gameState = GameState.Won;
			return;
		}

		if (winCameraTween != null)
		{
			winCameraTween.Kill();
			winCameraTween = null;
		}

		Vector2 currentCameraPosition = playerCamera.GlobalPosition;
		playerCamera.TopLevel = true;
		playerCamera.GlobalPosition = currentCameraPosition;
		playerCamera.PositionSmoothingEnabled = false;

		Vector2 viewportSize = GetViewportRect().Size;
		// Use Min so the smaller axis determines zoom, ensuring the full rect fits in frame.
		// In Godot 4 a zoom < 1 means zoomed out (more world visible).
		float zoomAmount = Mathf.Min(viewportSize.X / focusRect.Size.X, viewportSize.Y / focusRect.Size.Y);
		zoomAmount = Mathf.Clamp(zoomAmount, WinMinZoom, WinMaxZoom);
		Vector2 targetZoom = new Vector2(zoomAmount, zoomAmount);
		Vector2 targetPosition = focusRect.GetCenter();

		winCameraTween = CreateTween();
		winCameraTween.SetTrans(Tween.TransitionType.Sine);
		winCameraTween.SetEase(Tween.EaseType.Out);
		winCameraTween.TweenProperty(playerCamera, "global_position", targetPosition, WinCameraDuration);
		winCameraTween.Parallel().TweenProperty(playerCamera, "zoom", targetZoom, WinCameraDuration);
		winCameraTween.Finished += OnWinCameraTweenFinished;
	}

	private bool TryGetWinFocusRect(out Rect2 focusRect)
	{
		focusRect = new Rect2();

		if (!TryGetLayerBounds("back", out Rect2 backBounds) || !TryGetLayerBounds("mountain", out Rect2 mountainBounds))
		{
			return false;
		}

		float visibleMountainHeight = Mathf.Max(backBounds.Position.Y - mountainBounds.Position.Y, WinMinimumFrameHeight);
		focusRect = new Rect2(
			new Vector2(mountainBounds.Position.X, mountainBounds.Position.Y),
			new Vector2(mountainBounds.Size.X, visibleMountainHeight)
		);
		return true;
	}

	private void OnWinCameraTweenFinished()
	{
		winCameraTween = null;
		gameState = GameState.Won;
	}

	private void CullAllPlatforms()
	{
		for (int i = spawnedPlatforms.Count - 1; i >= 0; i--)
		{
			Node2D platform = spawnedPlatforms[i];
			if (!IsInstanceValid(platform))
			{
				continue;
			}

			platform.QueueFree();
		}

		spawnedPlatforms.Clear();
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

		var randomScene = platformScenes[GD.Randi() % (uint)platformScenes.Length];
		var platform = randomScene.Instantiate<Node2D>();
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

		if (!TryGetLayerBounds("back", out Rect2 bounds))
		{
			return false;
		}

		left = bounds.Position.X;
		right = bounds.End.X;
		top = bounds.Position.Y;
		bottom = bounds.End.Y;
		return true;
	}

	private bool TryGetLayerBounds(string layerName, out Rect2 bounds)
	{
		bounds = new Rect2();

		var layer = GetNodeOrNull<Node2D>(layerName);
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

		bounds = new Rect2(
			new Vector2(sprite.GlobalPosition.X - width * 0.5f, sprite.GlobalPosition.Y - height * 0.5f),
			new Vector2(width, height)
		);
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
