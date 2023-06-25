using UnityEngine;

public class WaterSim_Vel : MonoBehaviour
{
    public WaterSim WaterSimulation;

    private WaterSimResources WaterSimulationResource;

    [SerializeField]
    private RenderTexture   VelocityTexture;
    private RenderTexture   PressureTexture;


    private const int TEXTURE_SIZE              = 1024;
    private const int SIMULATION_DIMENSION      = TEXTURE_SIZE / 2;

    public void Start()
    {
        if(!SystemInfo.supportsAsyncGPUReadback) { this.gameObject.SetActive(false); return;}


        WaterSimulation.Initialize();
        WaterSimulationResource = new WaterSimResources(WaterSimulation);
        WaterSimulationResource.Create();

        // Create Texture for visualizing presure or velocity
        PressureTexture = new RenderTexture((int) TEXTURE_SIZE, (int) TEXTURE_SIZE, 0)
        {
            enableRandomWrite   = true,
            useMipMap           = true,
            graphicsFormat      = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SNorm,
            filterMode          = FilterMode.Trilinear,
            anisoLevel          = 7,
            format              = RenderTextureFormat.RFloat,
            wrapMode            = TextureWrapMode.Clamp
        };
        PressureTexture.Create();

        VelocityTexture = new RenderTexture((int) TEXTURE_SIZE, (int) TEXTURE_SIZE, 0)
        {            
            enableRandomWrite = true,
            useMipMap = true,
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16_SNorm,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 7,
            format = RenderTextureFormat.ARGBFloat,
            wrapMode = TextureWrapMode.Clamp,
        };
        VelocityTexture.Create();

        Vector2 WaterPipePosition       = new Vector2(SIMULATION_DIMENSION / 2, SIMULATION_DIMENSION - SIMULATION_DIMENSION * 0.1f);
        Vector2 WaterPipeDirectioon     = new Vector2(0.0f, -1.0f);

        // WaterSimulation.TestGridColor(WaterSimulationResource.VelocityBuffer);
        WaterSimulation.TestAddColor(WaterSimulationResource.VelocityBuffer);
        
        WaterSimulation.HandleCornerBoundaries(WaterSimulationResource.VelocityBuffer, FieldType.Velocity);
        
        WaterSimulation.Diffuse(WaterSimulationResource.VelocityBuffer);
        WaterSimulation.TestPipeMethod(WaterSimulationResource.VelocityBuffer);           

        WaterSimulation.Advection(WaterSimulationResource.VelocityBuffer, WaterSimulationResource.VelocityBuffer, 0.999f);
        
        // WaterSimulation.Visualiuse(WaterSimulationResource.VelocityBuffer);
        WaterSimulation.Visualiuse(WaterSimulationResource.VelocityBuffer, VelocityTexture);

        WaterSimulation.CopyVelocityBufferToTexture(VelocityTexture, WaterSimulationResource.VelocityBuffer);
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
