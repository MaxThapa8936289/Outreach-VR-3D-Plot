#region SimpleXboxControllerInput.cs - READ ME
// SimpleXboxControllerInput.cs
// Primary Functionality:
//      - To move the player based on input from the controller
//      - To update colour scales or cycle distributuions based on input from the controller
//
// Assignment Object: "Player"
// 
// Notes: 
//      Axes run from -1 to +1 and are pressure sensitive
#endregion

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleXboxControllerInput : MonoBehaviour
{
    // scripts to communicate with
    private CyclePlots CyclePlots;
    private NBodyPlotter NBodyPlotter;
    private GameObject _Camera;

    public static float moveSpeed = 10f;    // base speed of the player, editable in the inspector
    private float speed;                    // speed of the player in runtime
    private float LeftStickX;               // strafing
    private float LeftStickY;               // forward/backward motion
    private float Triggers;                 // ascending(right trigger)/descending(left trigger)

    [HideInInspector]
    public bool movementOnly = false;       // toggles wether the controller can only move or has full input control;

    private bool aButton;                   // hold to increase speed
    private bool bButtonDown;               // previous particle data file
    private bool xButtonDown;               // next particle data file
    private bool yButtonDown;               // unused
    private bool LeftBumperDown;            // increase colour scaling precision
    private bool RightBumperDown;           // decrease colour scaling precision

    void Start()
    {
        // Fetch the needed scripts from the "Player" object
        GameObject Player = GameObject.FindGameObjectWithTag("Player");
        CyclePlots = Player.GetComponent<CyclePlots>();

        // Fetch the needed scripts from the "MainCamera" object
        _Camera = GameObject.FindGameObjectWithTag("MainCamera");
        NBodyPlotter = _Camera.GetComponent<NBodyPlotter>();
    }

    void Update()
    {
        LeftStickX = -Input.GetAxis("Horizontal"); // left is +1, right is -1
        LeftStickY = -Input.GetAxis("Vertical");   // up is +1, down is -1

        // GetKey = active while held; GetKeyDown = active during the frame in which the button is pressed.
        // See http://wiki.unity3d.com/index.php?title=Xbox360Controller for button mappings (diagram here http://wiki.unity3d.com/index.php/File:X360Controller2.png)
        aButton = Input.GetKey(KeyCode.Joystick1Button0);
        bButtonDown = Input.GetKeyDown(KeyCode.Joystick1Button1);
        xButtonDown = Input.GetKeyDown(KeyCode.Joystick1Button2);
        yButtonDown = Input.GetKeyDown(KeyCode.Joystick1Button3);
        LeftBumperDown = Input.GetKeyDown(KeyCode.Joystick1Button4);
        RightBumperDown = Input.GetKeyDown(KeyCode.Joystick1Button5);

        // right trigger is represented by range -1 to 0, left trigger by range 0 to 1
        Triggers = -Input.GetAxis("Triggers");


        // Movement
        Vector3 Forward = _Camera.transform.TransformDirection(Vector3.forward);
        Vector3 Left = _Camera.transform.TransformDirection(Vector3.left);
        Vector3 Up = _Camera.transform.TransformDirection(Vector3.up);

        speed = (aButton) ? moveSpeed * 1.7f : moveSpeed;
        if (LeftStickY != 0.0f)
        {
            transform.Translate(LeftStickY * Forward * speed * Time.deltaTime);
        }
        if (LeftStickX != 0.0f)
        {
            transform.Translate(LeftStickX * Left * speed * Time.deltaTime);
        }
        if (Triggers != 0.0f)
        {
            transform.Translate(Triggers * Up * speed * Time.deltaTime);
        }

        if (!movementOnly)
        {
            // Change neighbour counting range (i.e. highlight precision)
            if (RightBumperDown) { NBodyPlotter.DoubleNeighbourRadius(); }
            if (LeftBumperDown) { NBodyPlotter.HalfNeighbourRadius(); }

            // Cycle Data files
            if (bButtonDown) { CyclePlots.PreviousParticleData(); }
            if (xButtonDown) { CyclePlots.NextParticleData(); }
        }

    }

}
