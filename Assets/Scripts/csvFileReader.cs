#region csvFileReader.cs - READ ME
// csvFileReader.cs
// Primary Functionality:
//      - To read a PERFECTLY FORMATTED .csv file line by line
//      - To parse each line into a float array of the form [x,y,z,vx,vy,vz,m]
//      - To add each parsed line to a list of float arrays (i.e. a list of each particles data)
//      - To return the list of particle data
//
// Assignment Object: NONE
// 
// Notes: 
//      This reader requires a PERFECTLY FORMATTED .csv file of the form
//          x,y,z,vx,vy,vz,m
//      every line. THERE IS NO ERROR CHECKING.
//      If the files have already been processed by either NBodySim_CsvOriginSetter_and_UnitScaler.exe or NBodySim_CsvShiftAndScaleFromFile.exe they should be fine.
#endregion

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class csvFileReader : MonoBehaviour
{
    public static List<float[]> parseCSV(string filename)
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, filename);
        List<float[]> parsedData = new List<float[]>();

        try
        {
            StreamReader readFile = new StreamReader(path);

            string line;
            string[] row;
            float[] f_row;

            while ((line = readFile.ReadLine()) != null)
            {
                row = line.Split(',');
                f_row = new float[row.Length];

                for (int i = 0; i < row.Length; i++)
                {
                    f_row[i] = Convert.ToSingle(row[i]);
                }
                parsedData.Add(f_row);
            }
            readFile.Close();
        }
        catch
        {
            Debug.Log("Error: Could not open file: " + filename);
        }

        return parsedData;
    }
}