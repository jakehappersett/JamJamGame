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
	private bool wasOnFloor = false;
	private bool isLanding = false;
	private bool facingRight = true;
	private AnimatedSprite2D animatedSprite2D;
	private Sprite2D defaultSprite2D;
	private bool controlsLocked = false;

	public override void _Ready()
	{
		animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		defaultSprite2D = GetNode<Sprite2D>("CollisionShape2D/Sprite2D");

		// Start in idle state: show the base sprite, hide run animation.
		defaultSprite2D.Visible = true;
		animatedSprite2D.Visible = false;
		wasOnFloor = IsOnFloor();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (controlsLocked)
		{
			Velocity = Vector2.Zero;
			return;
		}

		Vector2 velocity = Velocity;
		float deltaF = (float)delta;
		bool jumpedThisFrame = false;

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
			isLanding = false;
			jumpedThisFrame = true;
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
		MoveAndSlide();

		bool isOnFloorNow = IsOnFloor();
		bool justLanded = !wasOnFloor && isOnFloorNow;
		ProcessAnimations(direction, isOnFloorNow, justLanded, jumpedThisFrame);
		wasOnFloor = isOnFloorNow;
	}

	private void ProcessAnimations(Vector2 direction, bool isOnFloorNow, bool justLanded, bool jumpedThisFrame)
	{
		float movementX = Mathf.Abs(direction.X) > 0.01f ? direction.X : Velocity.X;
		if (Mathf.Abs(movementX) > 0.01f)
		{
			facingRight = movementX > 0f;
		}

		if (justLanded)
		{
			isLanding = true;
			PlayAnimated(facingRight ? "landing_right" : "landing_left");
			return;
		}

		if (isLanding)
		{
			if (!isOnFloorNow)
			{
				isLanding = false;
			}
			else if (animatedSprite2D.IsPlaying() && animatedSprite2D.Animation.ToString().StartsWith("landing_"))
			{
				return;
			}
			else
			{
				isLanding = false;
			}
		}

		if (!isOnFloorNow || jumpedThisFrame)
		{
			if (Velocity.Y > 0f)
			{
				string currentAnimation = animatedSprite2D.Animation.ToString();
				string transitionAnimation = facingRight ? "air_transition_right" : "air_transition_left";
				string fallingAnimation = facingRight ? "falling_right" : "falling_left";

				if (currentAnimation == transitionAnimation && animatedSprite2D.IsPlaying())
				{
					return;
				}

				if (jumpedThisFrame || currentAnimation.StartsWith("air_") || currentAnimation.StartsWith("jump_"))
				{
					PlayAnimated(transitionAnimation);
					return;
				}

				PlayAnimated(fallingAnimation);
				return;
			}

			if (jumpedThisFrame)
			{
				PlayAnimated(facingRight ? "jump_right" : "jump_left");
				return;
			}

			string jumpAnimation = facingRight ? "jump_right" : "jump_left";
			if (animatedSprite2D.IsPlaying() && animatedSprite2D.Animation.ToString() == jumpAnimation)
			{
				return;
			}

			PlayAnimated(facingRight ? "air_right" : "air_left");
			return;
		}

		if (Mathf.Abs(movementX) > 0.01f)
		{
			PlayAnimated(facingRight ? "run_right" : "run_left");
		}
		else
		{
			animatedSprite2D.Visible = false;
			defaultSprite2D.Visible = true;
			animatedSprite2D.Stop();
		}
	}

	private void PlayAnimated(string animationName)
	{
		defaultSprite2D.Visible = false;
		animatedSprite2D.Visible = true;

		if (animatedSprite2D.Animation != animationName)
		{
			animatedSprite2D.Play(animationName);
			return;
		}

		if (!animatedSprite2D.IsPlaying())
		{
			animatedSprite2D.Play();
		}
	}

	public void FreezeForWin()
	{
		controlsLocked = true;
		Velocity = Vector2.Zero;
		isJumpHeld = false;
		isLanding = false;

		if (animatedSprite2D != null && animatedSprite2D.IsPlaying())
		{
			animatedSprite2D.Stop();
		}
	}
}
