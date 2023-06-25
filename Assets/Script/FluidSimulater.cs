using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class FluidSimulater
{
    [Header("Compute Shader Refs")]
    [Space(2)]
    public ComputeShader SolversShader;
    public ComputeShader UserInputShader;
    public ComputeShader StructuredBufferToTextureShader;
    public ComputeShader PipeMethodShader;
    public ComputeShader StructuredBufferUtilityShader;
    public ComputeShader ShallowWaterEquationsShader;
    public ComputeShader WaveShader;

    private CommandBuffer SimCommandBuffer;

    [Space(2)]
    [Header("Materials")]
    [Space(1)]
    public Material m_outFlowMat;

    [Space(2)]
    [Header("Simulation Setting")]
    [Space(1)]
    public float DyeRadius = 1.0f;
    public float DyeFalloff = 2.0f;
    public float time_step = 1;
    public float Viscosity = 0.5f;

    [Space(2)]
    [Header("Control Settings")]
    [Space(1)]
    public KeyCode ApplyDyeKey;

    public float WaterDamping = 1.0f;

    [Space(2)]
    [Header("Fluid Simulation Setting")]
    [Space(1)]
    private const int TEXT_SIZE = 1024;
    private const int TOTAL_GRID_SIZE = 512;
    private const float TIME_STEP = 0.1f;
    private const int GRID_SIZE = 128;
    private const float PIPE_LENGTH = 1.0f;
    private const float CELL_LENGTH = 1.0f;
    private const float CELL_AREA = 1.0f; // CELL_LENGTH*CELL_LENGTH
    private const float GRAVITY = 9.81f;
    private const int READ = 0;
    private const int WRITE = 1;

    // private
    private Camera main_cam;
    [SerializeField]
    private RenderTexture VisualisationTexture;
    
    // Handles kernel
    private int _handle_add_dye;
    private int _handle_dye_st2tx;
    private int _handle_advection;
    private int _handle_Copy_StructuredBuffer;
    private int _handle_Clear_StructuredBuffer;
    private int _handle_wave_operation;

    private int _handle_jacob_solve;

    // Info used for input through mouse
    private Vector2 MousePreviousPos;

    public void Initialize()
    {
        ComputeShaderUtility.Initialize();
        WaterDamping = Mathf.Clamp01(WaterDamping);

        main_cam = Camera.main;
        if (main_cam == null) Debug.LogError("Could not find main camera, make sure the camera is tagged as main");

        MousePreviousPos = GetCurrentMouseInSimulation();

        VisualisationTexture = new RenderTexture(TEXT_SIZE, TEXT_SIZE, 0)
        {
            enableRandomWrite = true,
            useMipMap = false
        };
        VisualisationTexture.Create();

        _handle_add_dye                         = ComputeShaderUtility.GetKernelHandle(UserInputShader, "AddDye");
        _handle_wave_operation                  = ComputeShaderUtility.GetKernelHandle(WaveShader, "WaveOperation");
        _handle_dye_st2tx                       = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "DyeStructeredToTextureBillinearRGB8");
        _handle_advection                       = ComputeShaderUtility.GetKernelHandle(ShallowWaterEquationsShader, "Advection");
        _handle_Copy_StructuredBuffer           = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader  , "Copy_StructuredBuffer");
        _handle_Clear_StructuredBuffer          = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader  , "Clear_StructuredBuffer");
        _handle_jacob_solve                     = ComputeShaderUtility.GetKernelHandle(SolversShader  , "Jacobi_Solve");

        UpdateRuntimeKernelParameters();

        StructuredBufferToTextureShader.SetInt("_Dye_Results_Resolution", TEXT_SIZE); 
        // StructuredBufferToTextureShader.SetTexture(_hand)       

        SimCommandBuffer = new CommandBuffer()
        {
            name = "Simulation_Command_Buffer"
        };
        // Global parametes that are immutable in runtime
        SimCommandBuffer.SetGlobalInt("_TextureSize", TEXT_SIZE);
        SimCommandBuffer.SetGlobalFloat("_TimeStep", TIME_STEP);
        SimCommandBuffer.SetGlobalFloat("_GridSize", GRID_SIZE);
    }

    public bool BindCommandBuffer()
    {
        main_cam.AddCommandBuffer(CameraEvent.AfterEverything, SimCommandBuffer);
        return true;
    }

    public int GetSimulationDimension()
    {
        return TEXT_SIZE;
    }
    private void SetBufferOnCommandList(CommandBuffer cb, ComputeBuffer buffer, string buffer_name)
    {
        cb.SetGlobalBuffer(buffer_name, buffer);
    }
    private void DispatchComputeOnCommandBuffer(CommandBuffer cb, ComputeShader toDispatch, int kernel, uint thread_num_x, uint thread_num_y, uint thread_num_z)
    {
        DispatchDimensions group_nums = ComputeShaderUtility.CheckGetDispatchDimensions(toDispatch, kernel, thread_num_x, thread_num_y, thread_num_z);
        cb.DispatchCompute(toDispatch, kernel, (int) group_nums.dispatch_x, (int) group_nums.dispatch_y, (int) group_nums.dispatch_z);

        // Debug
        Debug.Log(string.Format("Attached the computeshader {0}, at kernel {1}, to the commandbuffer {2}." + "Dispatch group numbers are, in x, y,z respectivly: {3}", toDispatch.name, ComputeShaderUtility.GetKernelNameFromHandle(toDispatch, kernel), cb.name,group_nums.ToString()));
    }

    private Vector2 GetCurrentMouseInSimulation()
    {
        Vector3 mouse_pos_pixel_coord = Input.mousePosition;
        Vector2 mouse_pos_normalized  = main_cam.ScreenToViewportPoint(mouse_pos_pixel_coord);
                mouse_pos_normalized  = new Vector2(Mathf.Clamp01(mouse_pos_normalized.x), Mathf.Clamp01(mouse_pos_normalized.y));
        return new Vector2(mouse_pos_normalized.x * TEXT_SIZE, mouse_pos_normalized.y * TEXT_SIZE);
    }

    private void UpdateRuntimeKernelParameters()
    {
        SetFloatOnAllShaders(Time.time, "_TimeStep");

        float randomHue = Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f) + Mathf.Sin(Time.time * 0.7f + 2.0f));
        randomHue = randomHue - Mathf.Floor(randomHue);
        UserInputShader.SetVector("_dye_color", Color.HSVToRGB(randomHue, Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f))*0.2f + 0.8f, Mathf.Abs(Mathf.Sin(Time.time * 0.7f + 2.0f)) * 0.2f + 0.5f));
        UserInputShader.SetFloat("_mouse_dye_radius", DyeRadius);
        UserInputShader.SetFloat("_mouse_dye_falloff", DyeFalloff);

        float MousePressed = 0.0f;

        if (Input.GetKey(ApplyDyeKey)) MousePressed = 1.0f;

        UserInputShader.SetFloat("_mouse_pressed", MousePressed);

        Vector2 MousePosStructPos = GetCurrentMouseInSimulation();

        UserInputShader.SetVector("_mouse_position", MousePosStructPos);
        UserInputShader.SetVector("_mouse_pos_current", MousePosStructPos);
        UserInputShader.SetVector("_mouse_pos_prev", MousePreviousPos);

        MousePreviousPos = MousePosStructPos;        
    }

    private void SetFloatOnAllShaders(float ToSet, string Name)
    {        
        SolversShader                       .SetFloat(Name, ToSet);        
        StructuredBufferToTextureShader     .SetFloat(Name, ToSet);
        UserInputShader                     .SetFloat(Name, ToSet);
        StructuredBufferUtilityShader       .SetFloat(Name, ToSet);
        PipeMethodShader                    .SetFloat(Name, ToSet);
        ShallowWaterEquationsShader         .SetFloat(Name, ToSet);
        WaveShader.SetFloat(Name, ToSet);
    }

    public void AddDye(ComputeBuffer DyeBuffer)
    {
        SetBufferOnCommandList(SimCommandBuffer, DyeBuffer, "_dye_buffer");
        DispatchComputeOnCommandBuffer(SimCommandBuffer, UserInputShader, _handle_add_dye, TEXT_SIZE, TEXT_SIZE, 1);
    }

    public void Advect(ComputeBuffer BufferToAdvect, ComputeBuffer VelocityBuffer)
    {
        SimCommandBuffer.SetGlobalFloat("_dissipationFactor", 0.99f);        

        SetBufferOnCommandList(SimCommandBuffer, VelocityBuffer, "_velocity_field_buffer");
        SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect, "_field_to_advect_buffer");
        SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing, "_new_advected_field");

        DispatchComputeOnCommandBuffer(SimCommandBuffer, ShallowWaterEquationsShader, _handle_advection, TEXT_SIZE, TEXT_SIZE, 1);
        
        SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing, "_Copy_Source");
        SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect,              "_Copy_Target");

        DispatchComputeOnCommandBuffer(SimCommandBuffer, StructuredBufferUtilityShader, _handle_Copy_StructuredBuffer, TEXT_SIZE * TEXT_SIZE, 1, 1);
        
        ClearBuffer(FluidGPUResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }

    public void Diffuse(ComputeBuffer BufferToAdvect)
    {
        float centerFactor = 1.0f / (Viscosity * time_step);
        float reciprocal_of_diagonal = (Viscosity * time_step) / (1.0f + 4.0f * (Viscosity * time_step));

        SimCommandBuffer.SetGlobalFloat("_Damping", 1.0f - WaterDamping);
        SimCommandBuffer.SetGlobalFloat("_A", CELL_AREA);
        SimCommandBuffer.SetGlobalFloat("_G", GRAVITY);
        SimCommandBuffer.SetGlobalFloat("_L", CELL_LENGTH);

        SimCommandBuffer.SetGlobalFloat("_centerFactor", centerFactor);
        SimCommandBuffer.SetGlobalFloat("_rDiagonal", reciprocal_of_diagonal);

        bool ping_as_results = false;

        for (int i = 0; i < 360; i++)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results)                     // Ping ponging back and forth to insure no racing condition. 
            {
                SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect,              "_b_buffer");
                SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect,              "_updated_x_buffer");
                SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing,  "_results");
            } else
            {
                SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing,  "_b_buffer");
                SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing,  "_updated_x_buffer");
                SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect,              "_results");
            }
            
            DispatchComputeOnCommandBuffer(SimCommandBuffer, SolversShader, _handle_jacob_solve, TEXT_SIZE, TEXT_SIZE, 1);
        }

        if (ping_as_results)                         // The Ping ponging ended on the helper buffer ping. Copy it to the BufferToAdvect buffer
        {
            Debug.Log("Diffuse Ended on a Ping Target, now copying over the Ping to the buffer which was supposed to be diffused");
            SetBufferOnCommandList(SimCommandBuffer, FluidGPUResources.BufferPing, "_Copy_Source");
            SetBufferOnCommandList(SimCommandBuffer, BufferToAdvect,             "_Copy_Target");
            DispatchComputeOnCommandBuffer(SimCommandBuffer, StructuredBufferUtilityShader, _handle_Copy_StructuredBuffer, TEXT_SIZE * TEXT_SIZE, 1, 1);
        }
        
        ClearBuffer(FluidGPUResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }

    public void WaveOperation (ComputeBuffer buffer)
    {
        SetBufferOnCommandList(SimCommandBuffer, buffer, "_wave_buffer");
        DispatchComputeOnCommandBuffer(SimCommandBuffer, WaveShader, _handle_wave_operation, TEXT_SIZE, TEXT_SIZE, 1);
    }

    private void ClearBuffer(ComputeBuffer buffer, Vector4 clear_value)
    {
        SimCommandBuffer.SetGlobalVector("_Clear_Value_StructuredBuffer", new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        SetBufferOnCommandList(SimCommandBuffer, buffer, "_Clear_Target_StructuredBuffer");
        DispatchComputeOnCommandBuffer(SimCommandBuffer, StructuredBufferUtilityShader, _handle_Clear_StructuredBuffer, TEXT_SIZE * TEXT_SIZE, 1, 1);
    }

    public void Visualiuse (ComputeBuffer BufferToVisualize)
    {
        SetBufferOnCommandList(SimCommandBuffer, BufferToVisualize, "_Dye_StructuredToTexture_Source_RBB8");
        StructuredBufferToTextureShader.SetTexture(_handle_dye_st2tx, "_Dye_StructuredToTexture_Results_RBB8", VisualisationTexture);

        DispatchComputeOnCommandBuffer(SimCommandBuffer, StructuredBufferToTextureShader, _handle_dye_st2tx, TEXT_SIZE, TEXT_SIZE, 1);

        SimCommandBuffer.Blit(VisualisationTexture, BuiltinRenderTextureType.CameraTarget);
    }

    public void Tick(float DeltaTime)
    {
        UpdateRuntimeKernelParameters();
    }

    public void Release()
    {
        VisualisationTexture.Release();
        ComputeShaderUtility.Release();
    }
}
