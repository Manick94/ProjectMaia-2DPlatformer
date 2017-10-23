using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine : MonoBehaviour
{

	private IState currentState;
	private IState previousState;

	public void ChangeState (IState newState)
	{
		if (currentState != null) {
			currentState.Exit ();
		}

		previousState = currentState;
		currentState = newState;

		currentState.Enter ();
	}

	public void Update ()
	{
		if (currentState != null) {
			currentState.Update ();
		}
	}

	public void SwitchToPreviousState ()
	{
		if (currentState != null && previousState != null) {
			currentState.Exit ();
			currentState = previousState;
			currentState.Enter ();
		}
	}
}
