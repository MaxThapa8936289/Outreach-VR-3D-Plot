#region Plot.cs - READ ME
// Plot.cs
// Primary Functionality:
//      - A custom class to store particle data in Vector 4 array format
//      - To return particle data on request
//
// Assignment Object: NONE
// 
// Notes: 
//      This is used by NbodyPlotter.cs and CyclePlots.cs to store particle data read in from .csv files.
//      The 4 vector array format is much faster to process than .csv parsing
#endregion

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plot
{
    // private members
    private string name;
    private Vector4[] positions;
    private Vector4[] velocities;

    // Constructors
    // Default constrctor
    // NOTE: There should never be a reason to call the default constructor
    public Plot()
    {
        this.name = "";
        this.positions = new Vector4[0];
        this.velocities = new Vector4[0];
    }

    // Paramaterised constuctor
    public Plot(ref Vector4[] _positions, ref Vector4[] _velocities, string _name)
    {
        // set the members
        this.name = _name;
        this.positions = _positions;
        this.velocities = _velocities;
    }

    // Methods
    // readDataIntoVecotors()
    // replaces the passed variables with member data
    public void readDataIntoVecotors(ref Vector4[] _positions, ref Vector4[] _velocities)
    {
        // check for some potentially fatal errors
        if (this.positions.Length != 0 && this.velocities.Length != 0 && this.positions.Length == this.velocities.Length )
        {
            // replace the passed variables with the member data of this object
            _positions = this.positions;
            _velocities = this.velocities;
        }
        else if (this.positions.Length != 0 && this.velocities.Length != 0) { Debug.Log("Error: this object has no data!!"); }  // for debugging purposses
        else { Debug.Log("Error: this.positions.Length != this.velocities.Length - Something very wrong!"); }                   // for debugging purposses
    }

    // getName()
    // returns the name of this object
    // should eb the filename of the stored file data
    public string getName()
    {
        return name;
    }
}
