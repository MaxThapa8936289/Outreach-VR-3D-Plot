#region NBodyPlotter.cs - READ ME
// NBodyPlotter.cs
// Primary Functionality:
//      - To plot an individual distribution of particles to a 3D space for a fly through experience
//      
//      - To take data from a file using csvFileReader.cs
//      - To read this data into a four vector which contains 3-position and mass [x,y,x,m] and another four vector which contains 3-velocities and an empty parameter [vx,vy,vz,u]
//      - To pass this data to GPU_NeighbourCalculator.compute to calculate the density distribution and store it in the empty parameter [u] of the velocity four vector
//          (specifically, [u] is the number of neighbours a given particle has within a particular range)
//      - To pass the position and velocity vectors to the particle shader which plots and colours the particles
//
//      - To continuously update the screen with the relative positions of the particles compared to the camera ( giving a fly through experience )
//      - To continuously update the screen with the other information that may be changed in runtime by Click.cs or SimpleXboxControllerInput.cs 
//
// Assignment Object: "Main Camera"
// 
// Notes: 
//      This is the 'Main Program'. It's essentially a hub for incoming and outgoing information to other scripts. It manages only one file or distibtion at a time so CyclePlots.cs is used to change files.
//      The compute shader "GPU_NeighbourCalculator.compute" is a modified version of the "integrateBodies.compute" shader used in the realtime NBodySimulation project.
//      Technically the 3-velocities are never used in this plotting program. I have kept them read in anyway in anticipation of future developement.
//      This is the code I worked on the most, and have written almost all of it. For that reason it is amateur and may be inefficient, fragile or poorly formatted, so I apologise in advance for that. 
//      I have tried to comment as much as possible so that my intentions are clear.
#endregion

using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;


// Must be attached to a camera because DrawProcedural is used to render the particles
[RequireComponent(typeof(Camera))]
public class NBodyPlotter : MonoBehaviour {

    // const variables //
    // for compute buffers
	const int READ = 0;
	const int WRITE = 1;
	const int DEFALUT_SIZE = 98304;

    // Public variables // (note: all initialisers are overriden by values in the Inspector tab in Unity)
    // Initial Condition (IC) options (drop down menu)
    private enum CONFIG { FILE, CUBE, TWO_CUBES, SQUARE_SPIRAL, SQUARE }; 

    // Resources //
    public Material m_particleMat;                      // particle material ( which holds the particle shader )                                                   
    public ComputeShader m_GPUNeighbourCalculator;      // Compute Shader ( where the physics calculations are processed )
    public FileInfo m_particleDataFile;                 // Csv file for initial condition from file
    
    private List<Plot> Plots;                           // Array for holding already rendered distributions for faster slideshows
    private CONFIG m_config = CONFIG.FILE;              // Initial Condition (IC). For slideshow, should be set to CONFIG.FILE
    [HideInInspector] public int m_numBodies;           // Number of particles for non-file ICs ( must be multiple of 256. Overridden in inspector )
    [HideInInspector] public float m_spacingScale;      // Spacing between particles plotted for non-file ICs, in units of [0.001]
    [HideInInspector] public float m_zOffset;           // Shift the distribution along the z-axis. The camera looks toward the origin from z = -90.
    [HideInInspector] public float m_neighbourRadius;   // The radius from a given particle within which its neighbours will be counted.
    [HideInInspector] public float m_defaultMass;       // Mass attributed to all particles for non-file ICs

    private float minNeighbours = 0;                    // The number of neighbours the most solitary particle has
    private float maxNeighbours = 0;                    // The number of neighbours the most popular particle has  
    private float rangeNeighbours = 0;                  // The difference between the two

	ComputeBuffer[] m_positions, m_velocities;

    // DISCALIMER BY MAX THAPA: Can't say I really understand compute shaders, so this is left from the code I extracted from the GPU GEMS article https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch31.html. 
    //                          p and q refer to the p and q in said article. Some more clues can be found in the compute shader. 
    //                          The numbers I have chosen were the only ones that avoided drastic physical errors. 
    //                          Presumably other numbers could be used if the compute shader was correctly built for the graphics card on this machine.
    // Note: q is the number of threads per body, and p*q should be 
    // less than or equal to 256.
    // p must match the value of NUM_THREADS in the IntegrateBodies shader
    int p = 256;        // Sets the width of the tile used in the simulation.
    int q = 1;          // Sets the height of the tile used in the simulation.


    // Initiliasing the program
    void OnEnable()  
	{
        // assign array of compute buffers
		m_positions = new ComputeBuffer[2]; 
		m_velocities = new ComputeBuffer[2];

        if (m_config != CONFIG.FILE)
        {
            // Make sure m_numBodies is divisible by 256. If not, add particles until it is.
            if (m_numBodies % 256 != 0)
            {
                while (m_numBodies % 256 != 0)
                    m_numBodies++;

                Debug.Log("NBodySim::OnEnable - numBodies must be a multiple of 256. Changing numBodies to " + m_numBodies);
            }

            // Set compute buffer sizes now that m_numBodies is defined
            m_positions[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_positions[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            m_velocities[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_velocities[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            // Call the appropriate plotting function given the inspector choice of configuration.
            // Note: Really this program is only designed to handle files, so m_config should always be set to CONFIG.FILE
            if (m_config == CONFIG.CUBE)
            {
                ConfigCube();
            }
            if (m_config == CONFIG.TWO_CUBES)
            {
                Config2Cubes();
            }
            if (m_config == CONFIG.SQUARE_SPIRAL)
            {
                ConfigSquareSpiral();
            }
            if (m_config == CONFIG.SQUARE)
            {
                ConfigSquare();
            }
        }
        else // if m_confiig == CONFIG.FILE
        {
            // Skip setting compute buffers for now, need to ascertain the value of m_numBodies from the file first.
            // Call ConfigFile() which will process the file into the compute buffers
            ConfigFile();
        }

        // Calculate the number of neighbours each particle has using the compute shader
        PerformNeighbourCalculation();
    }


    #region INITIAL CONDITION CONFIGURATIONS

    // Here we will process the file data and store it into four vectors. 
    // Reading the .csv files with the basic reader I have written is slow (over 1 second per 100,000 particles) and is annoying for slideshows
    // To mitigate this issue, I have created a way to 'store' the file data after they have been read in the first time. They can then be accessed again much faster.
    //      When a file is succesfully processed, the four vectors are copied into a custom class called "Plot". An array of Plot class objects called "Plots" then 'stores' the vector data.
    //      If this file is requested to be loaded again by CyclePlots.cs, the stored version is called instead which bybasses the use of csvFileReader.cs and makes plotting much faster. 
    //      Thus, after game start, each file must be processed exactly once, upon request of that file.
    // A better solution mught be to write a faster .csv reader, or use a faster file format to .csv
    void ConfigFile()
    {
        // local variables
        bool stored = false;    // bool to check wether a plot has been saved for fast loading or not
        int plotIndex = 0;      // index to note down the location of a distribution in the array of Plot class objects.

        // Check the file name against the list of stored file names. If it can be found it must be stored.
        // NOTE: on game start Plots.Count is 0
        for (int i = 0; i < Plots.Count; i++)
        {
            if (m_particleDataFile.Name == Plots[i].getName()) { stored = true; plotIndex = i; break; }
        }
        // Here we put data into the compute buffers, ready for calculation.
        if (!stored)    // if the file is being called for the first time
        {
            // Parse the file to a list of floating point arrays of the form [x,y,z,vx,vy,vz,m]
            List<float[]> ParticleData = csvFileReader.parseCSV(m_particleDataFile.Name);

            // Assign the number of particles in the file to m_numBodies
            m_numBodies = ParticleData.Count;

            // Check wether m_numbBodies is compatible with the compute shader. 
            // If not, we cull extra particles
            if (m_numBodies % 256 != 0)
            {
                while (m_numBodies % 256 != 0)
                {
                    m_numBodies--;
                }
                Debug.Log("NBodySim::Start - numBodies must be a multiple of 256. Changing numBodies to " + m_numBodies);
            }
            
            // Now we have determined m_numBodies and made sure it is valid, we can create the compute buffers.
            m_positions[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_positions[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            m_velocities[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_velocities[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            // Create the 4 vector arrays to store the particle data 
            Vector4[] positions = new Vector4[m_numBodies];    
            Vector4[] velocities = new Vector4[m_numBodies];

            // Assign the particle data to each 4 vector
            for (int i = 0; i < m_numBodies; i++)
            {
                positions[i] = new Vector4(ParticleData[i][0], ParticleData[i][1], ParticleData[i][2] + m_zOffset, ParticleData[i][6]);
                velocities[i] = new Vector4(ParticleData[i][3], ParticleData[i][4], ParticleData[i][5], 0.0f);  // [u] is set to zero initially
            }
            
            // Now we store the 4 vector arrays using the constructor of the Plot class.
            Debug.Log("Storing slide");
            Plots.Add(new Plot(ref positions, ref velocities, m_particleDataFile.Name));

            // set the data of the compute buffers to the 4 vector arrays. After this we can perform calculations and rendering.
            m_positions[READ].SetData(positions);
            m_positions[WRITE].SetData(positions);

            m_velocities[READ].SetData(velocities);
            m_velocities[WRITE].SetData(velocities);
        }
        else    // If the file has been processed before during this runtime
        {
            Debug.Log("Loading from store");
            Debug.Log("plotIndex = " + plotIndex);

            // create some empty 4 vector arrays to be assigned
            Vector4[] positions = new Vector4[0];
            Vector4[] velocities = new Vector4[0];

            // assign the vectors using the Plot.readDataIntoVectore() function
            Plots[plotIndex].readDataIntoVecotors(ref positions, ref velocities);
            Debug.Log("positions.Length = " + positions.Length);

            // set m_numBodies. No need to check for compatibility here, it should already be of the correct form.
            m_numBodies = positions.Length;

            // Now we have determined m_numBodies, we can create the compute buffers.
            m_positions[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_positions[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            m_velocities[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_velocities[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            // Set the data of the compute buffers to the 4 vector arrays. After this we can perform calculations and rendering.
            m_positions[READ].SetData(positions);
            m_positions[WRITE].SetData(positions);

            m_velocities[READ].SetData(velocities);
            m_velocities[WRITE].SetData(velocities);
        }
        Debug.Log("Plots.count = " + Plots.Count);
    }

    // NOTE: These (non-file) distributions aren't used for this static plotting program. As such, the code is simply copied across from the real-time simulation project.
    //       See that project for full comments.
    void Config2Cubes()
    {
        int half_m_numBodies = m_numBodies / 2;
        double d_Cbrt_half_m_numBodies = Mathf.Pow(half_m_numBodies, 1f / 3f);
        int i_Cbrt_half_m_numBodies = (int)d_Cbrt_half_m_numBodies;
        float spacing = 0.001f * m_spacingScale;
        float sideLength = (i_Cbrt_half_m_numBodies) * spacing;
        float separation = 5.0f*sideLength;
        Vector3 yAxis = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 zAxis = new Vector3(0.0f, 0.0f, 1.0f);

        Vector4[] positions = new Vector4[m_numBodies];
        Vector4[] velocities = new Vector4[m_numBodies];

        Debug.Log("N = " + m_numBodies);
        Debug.Log("Double Cbrt of N/2 = " + d_Cbrt_half_m_numBodies);
        Debug.Log("Int Cbrt of N/2 = " + i_Cbrt_half_m_numBodies);
        Debug.Log("Vector size = " + positions.Length);

        for (int l = 1; l <= 2; l++)
        {
            float x_offset = separation / 2;
            if( l == 2 ) { x_offset = -separation / 2; }
            for (int i = 1; i <= i_Cbrt_half_m_numBodies; i++)
            {
                for (int j = 1; j <= i_Cbrt_half_m_numBodies; j++)
                {
                    for (int k = 1; k <= i_Cbrt_half_m_numBodies; k++)
                    {
                        int index = ((i - 1) * (int)Mathf.Pow(i_Cbrt_half_m_numBodies, 2) + (j - 1) * i_Cbrt_half_m_numBodies + (k - 1));
                        if (l == 2) { index += half_m_numBodies; }
                        float xpos;
                        float ypos;
                        float zpos;
                        xpos = ((2 * i) - (i_Cbrt_half_m_numBodies)) * spacing;
                        ypos = ((2 * j) - (i_Cbrt_half_m_numBodies)) * spacing;
                        zpos = ((2 * k) - (i_Cbrt_half_m_numBodies)) * spacing;

                        Vector3 pos = new Vector3(xpos + x_offset, ypos, zpos + m_zOffset);
                        Vector3 vel = Vector3.Cross(pos, zAxis);
                        //Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);
                        vel.x = Mathf.Pow(vel.x, 2) * Mathf.Sign(vel.x);
                        vel.y = Mathf.Pow(vel.y, 2) * Mathf.Sign(vel.y);
                        vel.z = Mathf.Pow(vel.z, 2) * Mathf.Sign(vel.z);

                        positions[index] = new Vector4(pos.x, pos.y, pos.z, m_defaultMass);
                        velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
                    }
                }
            }
            for (int i = (int)Mathf.Pow(i_Cbrt_half_m_numBodies, 3); i < half_m_numBodies; i++)
            {
                int index = i;
                if (l == 2) { index += half_m_numBodies; }
                Vector3 pos = new Vector3(Random.Range(-sideLength, sideLength), Random.Range(-sideLength, sideLength), Random.Range(-sideLength, sideLength));

                //Vector3 vel = Vector3.Cross(pos, zAxis);
                Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

                positions[index] = new Vector4(pos.x + x_offset, pos.y, pos.z + m_zOffset, m_defaultMass);
                velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
            }
        }

        m_positions[READ].SetData(positions);
        m_positions[WRITE].SetData(positions);

        m_velocities[READ].SetData(velocities);
        m_velocities[WRITE].SetData(velocities);
    }

    void ConfigCube()
    {
        double d_Cbrt_m_numBodies = Mathf.Pow(m_numBodies, 1f/3f);
        int i_Cbrt_m_numBodies = (int)d_Cbrt_m_numBodies;
        float spacing = 0.001f * m_spacingScale;
        float sideLength = (i_Cbrt_m_numBodies) * spacing;
        Vector3 yAxis = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 zAxis = new Vector3(0.0f, 0.0f, 1.0f);

        Vector4[] positions = new Vector4[m_numBodies];
        Vector4[] velocities = new Vector4[m_numBodies];

        Debug.Log("N = " + m_numBodies);
        Debug.Log("Double Cbrt of N = " + d_Cbrt_m_numBodies);
        Debug.Log("Int Cbrt of N = " + i_Cbrt_m_numBodies);
        Debug.Log("Vector size = " + positions.Length);

        for (int i = 1; i <= i_Cbrt_m_numBodies; i++)
        {
            for (int j = 1; j <= i_Cbrt_m_numBodies; j++)
            {
                for (int k = 1; k <= i_Cbrt_m_numBodies; k++)
                {
                    int index = (i - 1) * (int)Mathf.Pow(i_Cbrt_m_numBodies,2) + (j - 1) * i_Cbrt_m_numBodies + (k - 1);
                    float xpos;
                    float ypos;
                    float zpos;
                    //if (i % 2 == 0 && j % 2 == 0)
                    //{
                    //    xpos = -((2 * i) - (i_Cbrt_m_numBodies)) * spacing;
                    //    ypos = -((2 * j) - (i_Cbrt_m_numBodies)) * spacing;
                    //    zpos = -((2 * k) - (i_Cbrt_m_numBodies)) * spacing;
                    //}
                    //else if (i % 2 == 0 && j % 2 != 0)
                    //{
                    //    xpos = -((2 * i) - (i_Cbrt_m_numBodies)) * spacing;
                    //    ypos =  ((2 * j) - (i_Cbrt_m_numBodies)) * spacing;
                    //    zpos =  ((2 * k) - (i_Cbrt_m_numBodies)) * spacing;
                    //}
                    //else if (i % 2 != 0 && j % 2 == 0)
                    //{
                    //    xpos =  ((2 * i) - (i_Cbrt_m_numBodies)) * spacing;
                    //    ypos = -((2 * j) - (i_Cbrt_m_numBodies)) * spacing;
                    //    zpos = -((2 * k) - (i_Cbrt_m_numBodies)) * spacing;
                    //}
                    //else
                    //{
                        xpos =  ((2 * i) - (i_Cbrt_m_numBodies)) * spacing;
                        ypos =  ((2 * j) - (i_Cbrt_m_numBodies)) * spacing;
                        zpos =  ((2 * k) - (i_Cbrt_m_numBodies)) * spacing;
                    //}

                    Vector3 pos = new Vector3(xpos, ypos, zpos);
                    Vector3 vel = Vector3.Cross(pos, zAxis);
                    //Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

                    positions[index] = new Vector4(xpos, ypos, zpos + m_zOffset, m_defaultMass);
                    velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
                }
            }
        }
        for (int i = (int)Mathf.Pow(i_Cbrt_m_numBodies,3); i < m_numBodies; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-sideLength, sideLength), Random.Range(-sideLength, sideLength), Random.Range(-sideLength, sideLength));

            //Vector3 vel = Vector3.Cross(pos, zAxis);
            Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

            positions[i] = new Vector4(pos.x, pos.y, pos.z + m_zOffset, m_defaultMass);
            velocities[i] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
        }

        m_positions[READ].SetData(positions);
        m_positions[WRITE].SetData(positions);

        m_velocities[READ].SetData(velocities);
        m_velocities[WRITE].SetData(velocities);
    }

    void ConfigSquareSpiral()
    {

        double d_Sqrt_m_numBodies = Mathf.Pow(m_numBodies, 0.5f);
        int i_Sqrt_m_numBodies = (int)d_Sqrt_m_numBodies;
        float spacing = 0.001f * m_spacingScale;
        Vector3 zAxis = new Vector3(0.0f, 0.0f, 1.0f);
        Vector4[] positions = new Vector4[m_numBodies];
        Vector4[] velocities = new Vector4[m_numBodies];

        Debug.Log("N = " + m_numBodies);
        Debug.Log("Double Sqrt of N = " + d_Sqrt_m_numBodies);
        Debug.Log("Int Sqrt of N = " + i_Sqrt_m_numBodies);
        Debug.Log("Vector size = " + positions.Length);

        int k = i_Sqrt_m_numBodies;
        int i = 1;
        int j = 1;
        bool increasing = true;
        while (k != 1)
        {
            int index;
            float xpos;
            float ypos;
            float zpos = 0.0f;
            while (i <= k && (i-i_Sqrt_m_numBodies) > -(k+1))
            {
                index = (i - 1) * i_Sqrt_m_numBodies + (j - 1);
                //Debug.Log(i + ":" + j + ":" + k + " (i-loop)");
                xpos = ((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                ypos = ((2 * j) - (i_Sqrt_m_numBodies)) * spacing;
                //Debug.Log("(" + xpos + "," + ypos + ")");

                Vector3 pos = new Vector3(xpos, ypos, 0.0f);
                //Vector3 vel = Vector3.Cross(pos, zAxis);
                Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

                positions[index] = new Vector4(xpos, ypos, zpos + m_zOffset, m_defaultMass);
                velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
                if (increasing) { i++; }
                else { i--; }
            }
            if (increasing) { i--; j++; }
            else { i++; j--; }
            while (j <= k && (j - i_Sqrt_m_numBodies) > -k)
            {
                index = (i - 1) * i_Sqrt_m_numBodies + (j - 1);
                xpos = ((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                ypos = ((2 * j) - (i_Sqrt_m_numBodies)) * spacing;

                Vector3 pos = new Vector3(xpos, ypos, 0.0f);
                //Vector3 vel = Vector3.Cross(pos, zAxis);
                Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

                positions[index] = new Vector4(xpos, ypos, zpos + m_zOffset, m_defaultMass);
                velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
                if (increasing) { j++; }
                else { j--; }
            }
            if (increasing) { i--; j--; k--; increasing = false; }
            else { i++; j++; increasing = true; }
        }

        m_positions[READ].SetData(positions);
        m_positions[WRITE].SetData(positions);

        m_velocities[READ].SetData(velocities);
        m_velocities[WRITE].SetData(velocities);
    }

    void ConfigSquare()
    {
        double d_Sqrt_m_numBodies = Mathf.Pow(m_numBodies, 0.5f);
        int i_Sqrt_m_numBodies = (int)d_Sqrt_m_numBodies;
        float spacing = 0.001f * m_spacingScale;
        Vector3 zAxis = new Vector3(0.0f, 0.0f, 1.0f);

        Vector4[] positions = new Vector4[m_numBodies];
        Vector4[] velocities = new Vector4[m_numBodies];

        Debug.Log("N = " + m_numBodies);
        Debug.Log("Double Sqrt of N = " + d_Sqrt_m_numBodies);
        Debug.Log("Int Sqrt of N = " + i_Sqrt_m_numBodies);
        Debug.Log("Vector size = " + positions.Length);

        for (int i = 1; i <= i_Sqrt_m_numBodies; i++)
        {
         
            for (int j = 1; j <= i_Sqrt_m_numBodies; j++)
           {
                int index = (i - 1) * i_Sqrt_m_numBodies + (j - 1);
                float xpos;
                float ypos;
                if (i % 2 == 0 && j % 2 == 0)
                {
                    xpos = -((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                    ypos = -((2 * j) - (i_Sqrt_m_numBodies)) * spacing;
                }
                else if (i % 2 == 0 && j % 2 != 0)
                {
                    xpos = -((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                    ypos = ((2 * j) - (i_Sqrt_m_numBodies)) * spacing;
                }
                else if (i % 2 != 0 && j % 2 == 0)
                {
                    xpos = ((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                    ypos = -((2 * j) - (i_Sqrt_m_numBodies)) * spacing;
                }
                else
                {
                    xpos = ((2 * i) - (i_Sqrt_m_numBodies)) * spacing;
                    ypos = ((2 * j) - (i_Sqrt_m_numBodies)) * spacing;
                }

                float zpos = 0.0f;
                //Debug.Log("(" + xpos + "," + ypos + ")");

                Vector3 pos = new Vector3(xpos, ypos, 0.0f);
                //Vector3 vel = Vector3.Cross(pos, zAxis);
                Vector3 vel = new Vector3(0.0f, 0.0f, 0.0f);

                positions[index] = new Vector4(xpos, ypos, zpos + m_zOffset, m_defaultMass);
                velocities[index] = new Vector4(vel.x, vel.y, vel.z, 1.0f);
            }
        }

        m_positions[READ].SetData(positions);
        m_positions[WRITE].SetData(positions);

        m_velocities[READ].SetData(velocities);
        m_velocities[WRITE].SetData(velocities);
    }

    #endregion

    #region FUNCTION DEFENITIONS

    // PerformNeighbourCalculation() function defenition
    // sets variables in GPU_NeighbourCalculator.compute, then orders it to operate on the compute buffers.
    // The result is density information for each particle stored in the [u] paramater of the velocity 4 vector.
    // A scale is then calculated based on the range of values that [u] takes acrosss the distribution.
    void PerformNeighbourCalculation()
    {
        // set variables
        m_GPUNeighbourCalculator.SetFloat("_NeighbourRadius", m_neighbourRadius);
        m_GPUNeighbourCalculator.SetInt("_NumBodies", m_numBodies);
        m_GPUNeighbourCalculator.SetVector("_ThreadDim", new Vector4(p, q, 1, 0));
        m_GPUNeighbourCalculator.SetVector("_GroupDim", new Vector4(m_numBodies / p, 1, 1, 0));
        m_GPUNeighbourCalculator.SetBuffer(0, "_ReadPos", m_positions[READ]);
        m_GPUNeighbourCalculator.SetBuffer(0, "_WritePos", m_positions[WRITE]);
        m_GPUNeighbourCalculator.SetBuffer(0, "_ReadVel", m_velocities[READ]);
        m_GPUNeighbourCalculator.SetBuffer(0, "_WriteVel", m_velocities[WRITE]);

        // perform the calculation
        m_GPUNeighbourCalculator.Dispatch(0, m_numBodies / p, 1, 1);

        Swap(m_positions);
        Swap(m_velocities);


        Vector4[] velocities = new Vector4[m_numBodies];    // create a temporary 4 vector array to hold the new velocity 4 vecotors
        float[] neighbours = new float[m_numBodies];        // create a tempoarary float array to hold the now calcualted [u] values
        m_velocities[READ].GetData(velocities);             // extract the data from the compute buffer

        // extract only the [u] values
        for (int i = 0; i < m_numBodies; i++)               
        {
            neighbours[i] = velocities[i][3];
        }

        // calculate the range of values [u] takes for use in colour scaling.
        minNeighbours = Mathf.Min(neighbours);
        maxNeighbours = Mathf.Max(neighbours);
        if (minNeighbours == m_numBodies) { minNeighbours--; }  // prevents an incorrect colour scale in SpriteParticleShader.shader
        rangeNeighbours = maxNeighbours - minNeighbours;
    }

    // Swap() function definition
    // swaps copies of the compute buffers around, so that while one is being displayed other one can be processed for the next frame.
    void Swap(ComputeBuffer[] buffer) 
	{
		ComputeBuffer tmp = buffer[READ];
		buffer[READ] = buffer[WRITE];
		buffer[WRITE] = tmp;
	}

    // SetPlots() function definition
    // Sets the Plots array to the passed list
    // used for communication with CyclePlots.cs
    public void SetPlots(List<Plot> _Plots)
    {
        Plots = _Plots;
    }

    // ReturnPlots() function definition
    // Return the current list of plots
    // used for communication with CyclePlots.cs
    public List<Plot> ReturnPlots()
    {
        return Plots;
    }

    // DoubleNeighbourRadius() function definition
    // Doubles the size of the radius within which neighbours are counted.
    // Used by inputs Click.cs and SimpleXboxControllerInput.cs
    public void DoubleNeighbourRadius()
    {
        // maximum countable range is 100 units. Recall that a typical flythrough distribution should be no larger than ~200 units in diameter
        if (m_neighbourRadius < 100)
        {
            m_neighbourRadius *= 2.0f;
            if (m_neighbourRadius > 100) { m_neighbourRadius = 100f; }

            PerformNeighbourCalculation();

            Debug.Log("Set m_neighbourRadius to " + m_neighbourRadius);
            Debug.Log("Range of neighbours is between " + minNeighbours + " & " + maxNeighbours);
        }
    }

    // HalfNeighbourRadius() function definition
    // Halves the size of the radius within which neighbours are counted.
    // Used by inputs Click.cs and SimpleXboxControllerInput.cs
    public void HalfNeighbourRadius()
    {
        if (m_neighbourRadius > 0.01)
        {
            m_neighbourRadius /= 2.0f;

            PerformNeighbourCalculation();

            Debug.Log("Set m_neighbourRadius to " + m_neighbourRadius);
            Debug.Log("Range of neighbours is between " + minNeighbours + " & " + maxNeighbours);
        }
    }

    #endregion

    // NOTE void Update() is not called in this program

    //  OnPostRender is called once per frame after all camera rendering is complete
    void OnPostRender () 
	{
        // set variable in SpriteParticleShader.shader
		m_particleMat.SetPass(0);
		m_particleMat.SetBuffer("_Positions", m_positions[READ]);
        m_particleMat.SetBuffer("_Velocities", m_velocities[READ]);
        m_particleMat.SetFloat("_MinNeighbours", minNeighbours);
        m_particleMat.SetFloat("_RangeNeighbours", rangeNeighbours);
        // plot the particles to the screen
        Graphics.DrawProcedural(MeshTopology.Points, m_numBodies);
	}

    // OnDisable release the compute buffers
    void OnDisable()
    {
        m_positions[READ].Release();
        m_positions[WRITE].Release();
        m_velocities[READ].Release();
        m_velocities[WRITE].Release();
    }
}
















