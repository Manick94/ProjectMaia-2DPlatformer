using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
	public float gravity;
	public float runSpeed;
	public float groundFriction;
	public float airFriction;
	public float jumpHeight;

	private CharacterController2D characterController;
	private Animator animator;
	private RaycastHit2D lastControllerColliderHit;
	private Vector2 velocity;
	private bool isMeleeAttacking;
	private bool isThrowAttacking;

	private float normalizedHorizontalSpeed = 0f;

	void Awake ()
	{
		characterController = GetComponent<CharacterController2D> ();
		animator = GetComponent<Animator> ();
		velocity = Vector2.zero;
		isMeleeAttacking = false;
		isThrowAttacking = false;
	}

	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		HandleInput ();
		HandleMovement ();
		HandleAttacks ();
		HandleAnimations ();

		Reset ();
	}

	#region Event Listeners

	void OnControllerCollider (RaycastHit2D hit)
	{
		if (hit.normal.y == 1f) {
			return;
		}
	}

	void OnTriggerEnterEvent (Collider2D col)
	{
		Debug.Log ("OnTriggerEnterEvent: " + col.gameObject.name);
	}

	void OnTriggerExitEvent (Collider2D col)
	{
		Debug.Log ("OnTriggerExitEvent: " + col.gameObject.name);
	}

	#endregion

	void HandleInput ()
	{
		#if UNITY_STANDALONE || UNITY_EDITOR

		if (Input.GetMouseButtonDown (0)) {
			isMeleeAttacking = true;
			return;
		} else if (Input.GetMouseButtonDown (1)) {
			isThrowAttacking = true;
		}

		if (Input.GetKey (KeyCode.D)) {
			normalizedHorizontalSpeed = 1f;

			if (transform.localScale.x < 0f) {
				transform.localScale = new Vector3 (-transform.localScale.x, transform.localScale.y, transform.localScale.z);
			}

		} else if (Input.GetKey (KeyCode.A)) {
			normalizedHorizontalSpeed = -1f;

			if (transform.localScale.x > 0f) {
				transform.localScale = new Vector3 (-transform.localScale.x, transform.localScale.y, transform.localScale.z);
			}
		} else {
			normalizedHorizontalSpeed = 0f;
		}

		if (Input.GetKey (KeyCode.W) && characterController.isGrounded && !animator.GetCurrentAnimatorStateInfo (0).IsName ("PlayerMeleeAttack")) {
			velocity.y = Mathf.Sqrt (2f * jumpHeight * -gravity);
		}

		if (Input.GetKey (KeyCode.S) && !animator.GetCurrentAnimatorStateInfo (0).IsName ("PlayerMeleeAttack")) {
			if (characterController.isGrounded) {
				velocity.y *= 3f;
			}

			characterController.ignoreOneWayPlatformsThisFrame = true;
		}

		#elif UNITY_ANDROID || UNITY_IOS

		#endif
	}

	void HandleMovement ()
	{
		float friction = characterController.isGrounded ? groundFriction : airFriction;

		if (isMeleeAttacking || animator.GetCurrentAnimatorStateInfo (0).IsName ("PlayerMeleeAttack")) {
			velocity.x = Mathf.Lerp (velocity.x, 0, Time.deltaTime * friction);
		} else {
			velocity.x = Mathf.Lerp (velocity.x, normalizedHorizontalSpeed * runSpeed, Time.deltaTime * friction);
		}

		velocity.y += gravity * Time.deltaTime;

		characterController.Move (velocity * Time.deltaTime);
		velocity = characterController.velocity;
	}

	void HandleAttacks ()
	{
		if (isMeleeAttacking) {
			
		} else if (isThrowAttacking) {
			
		}
	}

	void HandleAnimations ()
	{
		if (characterController.isGrounded) {
			velocity.y = 0f;

			if (isMeleeAttacking && !animator.GetCurrentAnimatorStateInfo (0).IsName ("PlayerMeleeAttack")) {
				velocity.x = 0f;
				animator.SetTrigger ("playerMeleeAttack");
				return;
			} else if (isThrowAttacking) {
				return;
			}

			if (Mathf.Abs (velocity.x) > Mathf.Epsilon) {
				animator.SetTrigger ("playerRun");
			} else {
				animator.SetTrigger ("playerIdle");
			}
		} else {
			animator.SetTrigger ("playerJump");
		}
	}

	void Reset ()
	{
		isMeleeAttacking = false;
		isThrowAttacking = false;
	}
}
