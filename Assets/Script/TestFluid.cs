using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestFluid : MonoBehaviour
{
    public FluidSimulater FluidSimulater;

    private FluidGPUResources Resources;

    void Start()
    {
        FluidSimulater.Initialize();
        Resources = new FluidGPUResources(FluidSimulater);
        Resources.Create();        
            
        
        // FluidSimulater.WaveOperation(Resources.DyeBuffer);

        FluidSimulater.AddDye       (Resources.DyeBuffer);
        FluidSimulater.Advect       (Resources.DyeBuffer, Resources.VelocityBuffer);
        FluidSimulater.Diffuse      (Resources.DyeBuffer);

        FluidSimulater.Visualiuse   (Resources.DyeBuffer);

        FluidSimulater.BindCommandBuffer();
    }

    void Update()
    {
        FluidSimulater.Tick(Time.deltaTime);
    }

    void OnDisable()
    {
        FluidSimulater.Release();
        Resources.Release();
    }
}
