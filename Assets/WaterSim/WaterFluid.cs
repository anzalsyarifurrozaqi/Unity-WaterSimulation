using UnityEngine;

public class WaterFluid : MonoBehaviour
{  
    public WaterSim WaterSimulation;

    private WaterSimResources WaterSimulationResource;

    void Start()
    {
        if(!SystemInfo.supportsAsyncGPUReadback) { this.gameObject.SetActive(false); return;}

        WaterSimulation.Initialize();

        // WaterSimulation.WaterData = GetComponent<WaterData>();

        WaterSimulationResource = new WaterSimResources(WaterSimulation);
        WaterSimulationResource.Create();
        
        // WaterSimulation.WaterDataInitialize(WaterSimulationResource.WaveBuffer);

        // WaterSimulation.SetupTerrainField(WaterSimulationResource.TerrainBuffer);

        // WaterSimulation.SetupWaterField(WaterSimulationResource.WaterBuffer, WaterSimulationResource.WaveBuffer);
        
        WaterSimulation.AddDye(WaterSimulationResource.DyeBuffer);
        WaterSimulation.Diffuse(WaterSimulationResource.DyeBuffer);
        WaterSimulation.HandleCornerBoundaries(WaterSimulationResource.DyeBuffer, FieldType.Dye);
        // WaterSimulation.Advection(WaterSimulationResource.DyeBuffer, WaterSimulationResource.DyeBuffer, 1f);
        
        WaterSimulation.Visualiuse(WaterSimulationResource.DyeBuffer);

        WaterSimulation.BindComputeBuffer();
    }

    void Update()
    {
        WaterSimulation.Tick();

        // ping_as_results = !ping_as_results;
        // WaterSimulation.SetupWaterField(WaterSimulationResource.WaterBuffer, ping_as_results);
    }

    void OnDisable()
    {
        WaterSimulation.Release();
        WaterSimulationResource.Release();
    }
}
