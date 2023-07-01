using UnityEngine;

public class WaterSim_Vel : MonoBehaviour
{
    public WaterSim WaterSimulation;

    private WaterSimResources WaterSimulationResource;  

    public void Start()
    {
        if(!SystemInfo.supportsAsyncGPUReadback) { this.gameObject.SetActive(false); return;}

        WaterSimulation.Initialize();
        WaterSimulationResource = new WaterSimResources(WaterSimulation);
        WaterSimulationResource.Create();

        WaterSimulation.TestSurfacePos(WaterSimulationResource.PressureBuffer);
    }
    void Update()
    {
        WaterSimulation.Tick(WaterSimulationResource.PressureBuffer);

        // ping_as_results = !ping_as_results;
        // WaterSimulation.SetupWaterField(WaterSimulationResource.WaterBuffer, ping_as_results);
    }

    void OnDisable()
    {
        WaterSimulation.Release();
        WaterSimulationResource.Release();
    }
}
