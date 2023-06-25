using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidGPUResources : MonoBehaviour
{
    [Header("Compute Buffers")]
    [Space(2)]

    public ComputeBuffer DyeBuffer;
    public ComputeBuffer VelocityBuffer;
    
    
    
    public static ComputeBuffer BufferPing;

    public static ComputeBuffer BufferWave;

    private int SimulationDimensions;

    public FluidGPUResources()
    {
        SimulationDimensions = 256;
    }

    public FluidGPUResources(FluidSimulater FSO)
    {
        SimulationDimensions = FSO.GetSimulationDimension();
    }

    public void Release()
    {
        DyeBuffer.Release();
        VelocityBuffer.Release();
        BufferPing.Release();
        BufferPing.Release();
    }

    public void Create()
    {
        DyeBuffer           = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        VelocityBuffer      = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        BufferPing          = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        BufferWave          = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
    }
}
