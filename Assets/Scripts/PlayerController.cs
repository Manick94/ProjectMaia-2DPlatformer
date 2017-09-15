using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
	public float gravity = -25f;
	public float runSpeed = 8f;
	public float groundFriction = 20f;
	public float airFriction = 5f;
	public float jumpHeight = 3f;

	private CharacterController2D characterController;
	private Animator animator;
	private RaycastHit2D lastControllerColliderHit;
	private Vector2 velocity;

	private float normalizedHorizontalSpeed = 0f;

	void Awake ()
	{
		characterController = GetComponent<CharacterController2D> ();
		animator = GetComponent<Animator> ();
	}

	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (characterController.isGrounded) {
			velocity.y = 0;
		} else {
			animator.Play ("PlayerJump");
		}

		HandleInput ();
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

		if (Input.GetKey (KeyCode.D)) {
			normalizedHorizontalSpeed = 1f;

			if (transform.localScale.x < 0f) {
				transform.localScale = new Vector3 (-transform.localScale.x, transform.localScale.y, transform.localScale.z);
			}

			if (characterController.isGrounded) {
				animator.Play ("PlayerRun");
			}
		} else if (Input.GetKey (KeyCode.A)) {
			normalizedHorizontalSpeed = -1f;

			if (transform.localScale.x > 0f) {
				transform.localScale = new Vector3 (-transform.localScale.x, transform.localScale.y, transform.localScale.z);
			}

			if (characterController.isGrounded) {
				animator.Play ("PlayerRun");
			}
		} else {
			normalizedHorizontalSpeed = 0f;

			if (characterController.isGrounded) {
				animator.Play ("PlayerIdle");
			}
		}

		if (Input.GetKeyDown (KeyCode.W) && characterController.isGrounded) {
			velocity.y = Mathf.Sqrt (2f * jumpHeight * -gravity);
			animator.Play ("PlayerJump");
		}

		float friction = characterController.isGrounded ? groundFriction : airFriction;
		velocity.x = Mathf.Lerp (velocity.x, normalizedHorizontalSpeed * runSpeed, Time.deltaTime * friction);
		velocity.y += gravity * Time.deltaTime;

		if (characterController.isGrounded && Input.GetKey (KeyCode.S)) {
			velocity.y *= 3f;
			characterController.ignoreOneWayPlatformsThisFrame = true;
		}

		#elif UNITY_ANDROID || UNITY_IOS

		#endif

		characterController.Move (velocity * Time.deltaTime);
		velocity = characterController.velocity;
	}
}
