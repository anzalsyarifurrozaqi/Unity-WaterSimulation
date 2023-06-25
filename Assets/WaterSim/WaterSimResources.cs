using UnityEngine;

public class WaterSimResources
{
    public ComputeBuffer DyeBuffer;
    public ComputeBuffer PressureBuffer;
    public ComputeBuffer VelocityBuffer;
    public ComputeBuffer DivergenceBuffer;
    public ComputeBuffer BoundaryVelocityOffsetBuffer;
    public ComputeBuffer TerrainBuffer;
    public ComputeBuffer WaterBuffer;
    public ComputeBuffer WaveBuffer;


    public static ComputeBuffer BufferPing;

    private int SimulationDimensions;
    public WaterSimResources()
    {
        SimulationDimensions = 1024;
    }

    public WaterSimResources(WaterSim WaterSimulation)
    {
        SimulationDimensions = WaterSimulation.GetSimulationDimension();
    }

    public void Create()
    {
        Debug.Log("simulation dimensions" + SimulationDimensions);
        
        DyeBuffer                                   = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        PressureBuffer                              = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        VelocityBuffer                              = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        BoundaryVelocityOffsetBuffer                = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        TerrainBuffer                               = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        WaterBuffer                                 = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        WaveBuffer                                  = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 6);
        BufferPing                                  = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
        DivergenceBuffer                            = new ComputeBuffer(SimulationDimensions * SimulationDimensions, sizeof(float) * 4);
    }

    public void Release()
    {
        DyeBuffer.Release();
        PressureBuffer.Release();
        VelocityBuffer.Release();
        DivergenceBuffer.Release();
        BoundaryVelocityOffsetBuffer.Release();
        TerrainBuffer.Release();
        WaterBuffer.Release();
        WaveBuffer.Release();
        BufferPing.Release();
    }
}
