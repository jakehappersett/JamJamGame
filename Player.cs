using Godot;
using System;

public partial class Player : CharacterBody2D
{
	// Movement
	public const float MaxSpeed = 300.0f;
	public const float Acceleration = 1000.0f;
	public const float Deceleration = 1200.0f;
	
	// Jump tuning - adjust these to refine the jump feel
	public const float JumpVelocity = -450.0f;
	public const float AirControlMultiplier = 0.6f;
	public const float CoyoteTimeSeconds = 0.1f;
	public const float FallGravityMultiplier = 1.2f;
	public const float JumpHoldGravityMultiplier = 0.5f;
	
	// State tracking
	private float coyoteTimer = 0f;
	private bool isJumpHeld = false;
	private AnimatedSprite2D animatedSprite2D;
	private Sprite2D defaultSprite2D;

	public override void _Ready()
	{
		animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		defaultSprite2D = GetNode<Sprite2D>("CollisionShape2D/Sprite2D");

		// Start in idle state: show the base sprite, hide run animation.
		defaultSprite2D.Visible = true;
		animatedSprite2D.Visible = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		float deltaF = (float)delta;

		// Update coyote timer (allows jumping for brief moment after leaving floor)
		if (IsOnFloor())
		{
			coyoteTimer = 0f;
		}
		else
		{
			coyoteTimer += deltaF;
		}

		// Add gravity with variable multiplier based on jump state
		if (!IsOnFloor())
		{
			float gravityMultiplier = 1.0f;
			
			// Apply reduced gravity while jump is held and moving upward (floaty feel)
			if (isJumpHeld && velocity.Y < 0f)
			{
				gravityMultiplier = JumpHoldGravityMultiplier;
			}
			// Apply increased gravity while falling (snappy descent)
			else if (velocity.Y > 0f)
			{
				gravityMultiplier = FallGravityMultiplier;
			}
			
			velocity += GetGravity() * gravityMultiplier * deltaF;
		}

		// Handle Jump with coyote time (jump window after leaving platform)
		if (Input.IsActionJustPressed("ui_accept") && (IsOnFloor() || coyoteTimer < CoyoteTimeSeconds))
		{
			velocity.Y = JumpVelocity;
			isJumpHeld = true;
		}

		// Track jump button state for variable jump height
		if (Input.IsActionJustReleased("ui_accept"))
		{
			isJumpHeld = false;
		}

		// Get the input direction and handle the movement/deceleration
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		
		// Determine acceleration/deceleration multiplier based on whether we're in the air (limited air control)
		float currentAcceleration = Acceleration;
		float currentDeceleration = Deceleration;
		if (!IsOnFloor())
		{
			currentAcceleration *= AirControlMultiplier;
			currentDeceleration *= AirControlMultiplier;
		}
		
		if (direction != Vector2.Zero)
		{
			velocity.X = Mathf.MoveToward(Velocity.X, direction.X * MaxSpeed, currentAcceleration * deltaF);
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, currentDeceleration * deltaF);
		}

		Velocity = velocity;
		ProcessAnimations(direction);
		MoveAndSlide();
	}

	private void ProcessAnimations(Vector2 direction)
	{
		float movementX = Mathf.Abs(direction.X) > 0.01f ? direction.X : Velocity.X;

		if (Mathf.Abs(movementX) > 0.01f)
		{
			defaultSprite2D.Visible = false;
			animatedSprite2D.Visible = true;

			string targetAnimation = movementX > 0f ? "run_right" : "run_left";

			if (animatedSprite2D.Animation != targetAnimation)
			{
				animatedSprite2D.Animation = targetAnimation;
			}

			if (!animatedSprite2D.IsPlaying())
			{
				animatedSprite2D.Play();
			}
		}
		else
		{
			animatedSprite2D.Visible = false;
			defaultSprite2D.Visible = true;
			animatedSprite2D.Stop();
		}
	}
}
