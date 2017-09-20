#region CyclePlots.cs - READ ME
// CyclePlots.cs
// Primary Functionality:
//      - To take variables from the inspector and inject them into the plotting script "NBodyPlotter.cs"
//      - To retreive and hold a list of data files found in /StreamingAssets/ for use as "slides" in the 3D plot
//      - To call the script "NBodyPlotter.cs" and so plot each slide
//      - To cycle through the slides
//      - To store plotted files in vector format for fast reloading 
//
// Assignment Object: "Player"
// 
// Notes: 
//      FILES CYCLE IN THE ORDER THEY ARE STORED IN STREAMING ASSETS. Name files accordingly to sort.
//      This is the 'Primary Script', so to speak; It provides vital information to, and calls, NBodyPlotter.cs which is the main program. 
//      It must finish initialising before NBodyPlotter.cs begins to run and so NBodyPlotter.cs should be disabled on game start.
//      The term "slide" is often used in place of "particle data" or "3D plot".
#endregion

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public class CyclePlots : MonoBehaviour {

    // const variables //
    const int DEFALUT_SIZE = 98304;

    // Scripts to communicate with
    private NBodyPlotter NBodyPlotter;

    // Array to hold Particle Data .csv files.
    private FileInfo[] ParticleData;

    // Public variables // (note: all initialisers are overriden by values in the Inspector tab in Unity)
    public int m_numBodies = DEFALUT_SIZE;          // Number of particles for non-file ICs ( must be multiple of 256. Overridden in inspector )
    public float m_spacingScale = 100.0f;           // Spacing between particles plotted for non-file ICs, in units of [0.001]
    public float m_zOffset = -50.0f;                // Shift the distribution along the z-axis. The camera looks toward the origin from z = -90.
    [Range(0.01f, 100.0f)]
    public float m_neighbourRadius = 10.0f;         // The radius from a given particle within which its neighbours will be counted.
    public float m_defaultMass = 1.0f;              // Mass attributed to all particles for non-file ICs

    // Private variables
    private int currentFileIndex = 0;               // integer index for the array of Particle Data files.
    private List<Plot> Plots;                       // List of 'Plot' class objects for fast slide re-loading

    // Initialising the program
    void Start ()
    {
        GameObject Camera = GameObject.FindGameObjectWithTag("MainCamera");             // Fetch the Camera object which is a child of the "Player" Object. 
        NBodyPlotter = Camera.GetComponent<NBodyPlotter>();                             // Assign the NBodyPlotter.cs script attached to the Camera object to variable name "NBodyPlotter"
        NBodyPlotter.enabled = false;                                                   // NBodyPlotter.cs should be disabled anyway but just in case...

        // Set the parameters from the inspector
        NBodyPlotter.m_numBodies = m_numBodies;
        NBodyPlotter.m_spacingScale = m_spacingScale;
        NBodyPlotter.m_zOffset = m_zOffset;
        NBodyPlotter.m_neighbourRadius = m_neighbourRadius;
        NBodyPlotter.m_defaultMass = m_defaultMass;

        Plots = new List<Plot>(0);                                                      // Initialise the list as empty

        DirectoryInfo dir = new DirectoryInfo(Application.streamingAssetsPath);         // Obtain the Directory path of the StreamingAssets folder bundles with the game build
        ParticleData = dir.GetFiles("*.csv");                                           // Populate the FileInfo array with the files in the directory. 

        NBodyPlotter.m_particleDataFile = ParticleData[currentFileIndex];               // Set the file to be loaded to the first one (currentFileIndex was initialised to 0)

        NBodyPlotter.SetPlots(Plots);                                                   // Set the variable 'Plots' in NBodyPlotter to the (empty) list held in this script.
        NBodyPlotter.enabled = true;                                                    // Launch the program!
        Plots = NBodyPlotter.ReturnPlots();                                             // Update the list of Plots with the loaded slide.
    }

    // Public functions. These are called from Click.cs and SimpleXboxControllerInput.cs
    // Plot the next file and update the list of Plots.
    public void NextParticleData()
    {
        NBodyPlotter.enabled = false;                                                   // Disable the plotting script so it can be re-enabled where all the plotting happens

        if (currentFileIndex == ParticleData.Length - 1) { currentFileIndex = 0; }      // Cycle the index
        else { currentFileIndex++; }                                                    // 
        NBodyPlotter.m_particleDataFile = ParticleData[currentFileIndex];               // Use the index to retrieve and set the new file.

        NBodyPlotter.SetPlots(Plots);                                                   // Set the variable 'Plots' in NBodyPlotter to the (populated) list held in this script. 
        NBodyPlotter.enabled = true;                                                    // Launch the program! Again!
        Plots = NBodyPlotter.ReturnPlots();                                             // Update the list of Plots. 
    }
    // Plot the previous file and update the list of Plots.
    public void PreviousParticleData()
    {
        NBodyPlotter.enabled = false;                                                   // Disable the plotting script so it can be re-enabled where all the plotting happens

        if (currentFileIndex == 0) { currentFileIndex = ParticleData.Length - 1; }      // Cycle the index (the other direction)
        else { currentFileIndex--; }                                                    //
        NBodyPlotter.m_particleDataFile = ParticleData[currentFileIndex];               // Use the index to retrieve and set the new file.

        NBodyPlotter.SetPlots(Plots);                                                   // Set the variable 'Plots' in NBodyPlotter to the (populated) list held in this script. 
        NBodyPlotter.enabled = true;                                                    // Launch the program! Again!
        Plots = NBodyPlotter.ReturnPlots();                                             // Update the list of Plots. 
    }
}
