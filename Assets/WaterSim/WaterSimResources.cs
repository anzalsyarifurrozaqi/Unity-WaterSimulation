using UnityEngine;

public class WaterSimResources
{        
    public ComputeBuffer PressureBuffer;

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
        
        PressureBuffer                              = new ComputeBuffer(SimulationDimensions, 8 * 4);
    }

    public void Release()
    {        
        PressureBuffer.Release();
    }
}
