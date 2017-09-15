#define DEBUG_CC2D_RAYS

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{

	#region Internal Types

	struct CharacterRaycastOrigins
	{
		public Vector2 topLeft;
		public Vector2 bottomRight;
		public Vector2 bottomLeft;
	}

	public class CharacterCollisionState2D
	{
		public bool right, left, above, below;
		public bool becameGroundedThisFrame;
		public bool wasGroundedLastFrame;
		public bool movingDownSlope;
		public float slopeAngle;

		public bool HasCollision ()
		{
			return right || left || above || below;
		}

		public void Reset ()
		{
			right = left = above = below = becameGroundedThisFrame = movingDownSlope = false;
			slopeAngle = 0f;
		}

		public override string ToString ()
		{
			return string.Format ("[CharacterCollisionState2D] " +
			"r: {0}, l: {1}, a: {2}, b: {3}, " +
			"movingDownSlope: {4}, slopeAngle: {5}, " +
			"becameGroundedThisFrame: {6}, wasGroundedLastFrame: {7}",
				right, left, above, below, 
				movingDownSlope, slopeAngle, 
				becameGroundedThisFrame, wasGroundedLastFrame);
		}
	}

	#endregion

	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		
	}

	#region Events, Properties, and Fields

	public event Action<RaycastHit2D> onControllerCollidedEvent;
	public event Action<Collider2D> onTriggerEnterEvent;
	public event Action<Collider2D> onTriggerStayEvent;
	public event Action<Collider2D> onTriggerExitEvent;

	public bool ignoreOneWayPlatformsThisFrame;

	[SerializeField]
	[Range (0.001f, 0.3f)]
	float _skinWidth = 0.02f;

	public float skinWidth {
		get{ return _skinWidth; }
		set {
			_skinWidth = value;
			RecalculateDistanceBetweenRays ();
		}
	}

	public LayerMask platformMask = 0;
	public LayerMask triggerMask = 0;
	public LayerMask oneWayPlatformMask = 0;

	[Range (0f, 90f)]
	public float slopeLimit = 30f;
	public AnimationCurve slopeSpeedMultiplier = new AnimationCurve (
		                                             new Keyframe (-90f, 1.5f), 
		                                             new Keyframe (0f, 1f), 
		                                             new Keyframe (90f, 0f));
	
	public float jumpThreshold = 0.07f;

	[Range (2, 20)]
	public int totalHorizontalRays = 8;
	[Range (2, 20)]
	public int totalVerticalRays = 4;

	[HideInInspector][NonSerialized]
	public new Transform transform;
	[HideInInspector][NonSerialized]
	public BoxCollider2D boxCollider;
	[HideInInspector][NonSerialized]
	public Rigidbody2D rigidBody;
	[HideInInspector][NonSerialized]
	public Vector2 velocity;
	[HideInInspector][NonSerialized]
	public CharacterCollisionState2D collisionState = new CharacterCollisionState2D ();

	public bool isGrounded { get { return collisionState.below; } }

	float slopeLimitTangent = Mathf.Tan (75f * Mathf.Deg2Rad);
	float verticalDistanceBetweenRays;
	float horizontalDistanceBetweenRays;

	bool isGoindUpSlope = false;

	const float skinWidthFloatFudgeFactor = 0.001f;

	CharacterRaycastOrigins raycastOrigins;
	RaycastHit2D raycastHit;
	List<RaycastHit2D> raycastHitsThisFrame = new List<RaycastHit2D> (2);

	#endregion

	#region MonoBehaviour

	void Awake ()
	{
		platformMask |= oneWayPlatformMask;

		transform = GetComponent<Transform> ();
		boxCollider = GetComponent<BoxCollider2D> ();
		rigidBody = GetComponent<Rigidbody2D> ();

		skinWidth = _skinWidth;

		for (var i = 0; i < 32; i++) {
			if ((triggerMask.value & 1 << i) == 0) {
				Physics2D.IgnoreLayerCollision (gameObject.layer, i);
			}
		}
	}

	public void OnTriggerEnter2D (Collider2D col)
	{
		if (onTriggerEnterEvent != null) {
			onTriggerEnterEvent (col);
		}
	}

	public void OnTriggerStay2D (Collider2D col)
	{
		if (onTriggerStayEvent != null) {
			onTriggerStayEvent (col);
		}
	}

	public void OnTriggerExit2D (Collider2D col)
	{
		if (onTriggerExitEvent != null) {
			onTriggerExitEvent (col);
		}
	}

	#endregion

	[System.Diagnostics.Conditional ("DEBUG_CC2D_RAYS")]
	void DrawRay (Vector2 start, Vector2 dir, Color color)
	{
		Debug.DrawRay (start, dir, color);
	}

	#region Public

	public void Move (Vector2 deltaMovement)
	{
		collisionState.wasGroundedLastFrame = collisionState.below;

		collisionState.Reset ();
		raycastHitsThisFrame.Clear ();
		isGoindUpSlope = false;

		PrimeRaycastOrigins ();

		if (deltaMovement.y < 0f && collisionState.wasGroundedLastFrame) {
			HandleVerticalSlope (ref deltaMovement);
		}

		if (deltaMovement.x != 0f) {
			MoveHorizontally (ref deltaMovement);
		}

		if (deltaMovement.y != 0f) {
			MoveVertically (ref deltaMovement);
		}

		transform.Translate (deltaMovement, Space.World);

		if (Time.deltaTime > 0f) {
			velocity = deltaMovement / Time.deltaTime;
		}

		if (!collisionState.wasGroundedLastFrame && collisionState.below) {
			collisionState.becameGroundedThisFrame = true;
		}

		if (isGoindUpSlope) {
			velocity.y = 0;
		}

		if (onControllerCollidedEvent != null) {
			for (int i = 0; i < raycastHitsThisFrame.Count; i++) {
				onControllerCollidedEvent (raycastHitsThisFrame [i]);
			}
		}

		ignoreOneWayPlatformsThisFrame = false;
	}

	public void WarpToGround ()
	{
		do {
			Move (new Vector2 (0, -1f));
		} while(!isGrounded);
	}

	public void RecalculateDistanceBetweenRays ()
	{
		float colliderUseableHeight = boxCollider.size.y * Mathf.Abs (transform.localScale.y) - (2f * skinWidth);
		verticalDistanceBetweenRays = colliderUseableHeight / (totalHorizontalRays - 1);

		float colliderUseableWidth = boxCollider.size.x * Mathf.Abs (transform.localScale.x) - (2f * skinWidth);
		horizontalDistanceBetweenRays = colliderUseableWidth / (totalVerticalRays - 1);
	}

	#endregion

	#region Movement Methods

	void PrimeRaycastOrigins ()
	{
		var modifiedBounds = boxCollider.bounds;
		modifiedBounds.Expand (-2f * skinWidth);

		raycastOrigins.topLeft = new Vector2 (modifiedBounds.min.x, modifiedBounds.max.y);
		raycastOrigins.bottomRight = new Vector2 (modifiedBounds.max.x, modifiedBounds.min.y);
		raycastOrigins.bottomLeft = modifiedBounds.min;
	}

	void MoveHorizontally (ref Vector2 deltaMovement)
	{
		var isGoingRight = deltaMovement.x > 0f;
		var rayDistance = Mathf.Abs (deltaMovement.x) + skinWidth;
		var rayDirection = isGoingRight ? Vector2.right : Vector2.left;
		var initialRayOrigin = isGoingRight ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;

		for (int i = 0; i < totalHorizontalRays; i++) {
			Vector2 ray = new Vector2 (initialRayOrigin.x, initialRayOrigin.y + i * verticalDistanceBetweenRays);
			DrawRay (ray, rayDirection * rayDistance, Color.red);

			if (i == 0 && collisionState.wasGroundedLastFrame) {
				raycastHit = Physics2D.Raycast (ray, rayDirection, rayDistance, platformMask);
			} else {
				raycastHit = Physics2D.Raycast (ray, rayDirection, rayDistance, platformMask & ~oneWayPlatformMask);
			}

			if (raycastHit) {
				if (i == 0 && HandleHorizontalSlope (ref deltaMovement, Vector2.Angle (raycastHit.normal, Vector2.up))) {
					raycastHitsThisFrame.Add (raycastHit);
					break;
				}

				deltaMovement.x = raycastHit.point.x - ray.x;
				rayDistance = Mathf.Abs (deltaMovement.x);

				if (isGoingRight) {
					deltaMovement.x -= skinWidth;
					collisionState.right = true;
				} else {
					deltaMovement.x += skinWidth;
					collisionState.left = true;
				}

				raycastHitsThisFrame.Add (raycastHit);

				if (rayDistance < skinWidth + skinWidthFloatFudgeFactor) {
					break;
				}
			}
		}
	}

	bool HandleHorizontalSlope (ref Vector2 deltaMovement, float angle)
	{
		if (Mathf.RoundToInt (angle) == 90) {
			return false;
		}

		if (angle < slopeLimit) {
			if (deltaMovement.y < jumpThreshold) {
				float slopeModifier = slopeSpeedMultiplier.Evaluate (angle);
				deltaMovement.x *= slopeModifier;

				deltaMovement.y = Mathf.Abs (Mathf.Tan (angle * Mathf.Deg2Rad) * deltaMovement.x);
				bool isGoingRight = deltaMovement.x > 0f;

				Vector2 ray = isGoingRight ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;

				RaycastHit2D raycastHit;
				if (collisionState.wasGroundedLastFrame) {
					raycastHit = Physics2D.Raycast (ray, deltaMovement.normalized, deltaMovement.magnitude, platformMask);
				} else {
					raycastHit = Physics2D.Raycast (ray, deltaMovement.normalized, deltaMovement.magnitude, platformMask & ~oneWayPlatformMask);
				}

				if (raycastHit) {
					deltaMovement = (Vector2)raycastHit.point - ray;

					if (isGoingRight) {
						deltaMovement.x -= skinWidth;
					} else {
						deltaMovement.x += skinWidth;
					}
				}

				isGoindUpSlope = true;
				collisionState.below = true;
			}
		} else {
			deltaMovement.x = 0f;
		}

		return true;
	}

	void MoveVertically (ref Vector2 deltaMovement)
	{
		bool isGoingUp = deltaMovement.y > 0f;
		float rayDistance = Mathf.Abs (deltaMovement.y) + skinWidth;
		Vector2 rayDirection = isGoingUp ? Vector2.up : Vector2.down;
		Vector2 initialRayOrigin = isGoingUp ? raycastOrigins.topLeft : raycastOrigins.bottomLeft;

		initialRayOrigin.x += deltaMovement.x;

		var mask = platformMask;
		if ((isGoingUp && !collisionState.wasGroundedLastFrame) || ignoreOneWayPlatformsThisFrame) {
			mask &= ~oneWayPlatformMask;
		}

		for (int i = 0; i < totalVerticalRays; i++) {
			Vector2 ray = new Vector2 (initialRayOrigin.x + i * horizontalDistanceBetweenRays, initialRayOrigin.y);
			DrawRay (ray, rayDirection * rayDistance, Color.yellow);

			raycastHit = Physics2D.Raycast (ray, rayDirection, rayDistance, mask);
			if (raycastHit) {
				deltaMovement.y = raycastHit.point.y - ray.y;
				rayDistance = Mathf.Abs (deltaMovement.y);

				if (isGoingUp) {
					deltaMovement.y -= skinWidth;
					collisionState.above = true;
				} else {
					deltaMovement.y += skinWidth;
					collisionState.below = true;
				}

				raycastHitsThisFrame.Add (raycastHit);

				if (!isGoingUp && deltaMovement.y > 0.00001f) {
					isGoingUp = true;
				}

				if (rayDistance < skinWidth + skinWidthFloatFudgeFactor) {
					break;
				}
			}
		}
	}

	void HandleVerticalSlope (ref Vector2 deltaMovement)
	{
		float centerOfCollider = (raycastOrigins.bottomLeft.x + raycastOrigins.bottomRight.x) * 0.5f;
		Vector2 rayDirection = Vector2.down;

		float slopeCheckRayDistance = slopeLimitTangent * (raycastOrigins.bottomRight.x - centerOfCollider);

		Vector2 slopeRay = new Vector2 (centerOfCollider, raycastOrigins.bottomLeft.y);
		DrawRay (slopeRay, rayDirection * slopeCheckRayDistance, Color.yellow);

		raycastHit = Physics2D.Raycast (slopeRay, rayDirection, slopeCheckRayDistance, platformMask);
		if (raycastHit) {
			float angle = Vector2.Angle (raycastHit.normal, Vector2.up);
			if (angle == 0f) {
				return;
			}

			bool isMovingDownSlope = Mathf.Sign (raycastHit.normal.x) == Mathf.Sign (deltaMovement.x);
			if (isMovingDownSlope) {
				float slopeModifier = slopeSpeedMultiplier.Evaluate (-angle);

				deltaMovement.y += raycastHit.point.y - slopeRay.y - skinWidth;
				deltaMovement.x *= slopeModifier;
				collisionState.movingDownSlope = true;
				collisionState.slopeAngle = angle;
			}
		}
	}

	#endregion
}
