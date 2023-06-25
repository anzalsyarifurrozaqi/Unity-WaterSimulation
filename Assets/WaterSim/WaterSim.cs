using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

using RenderTextureUtility;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
struct VertexData
{
    public Vector3 pos;
    public Vector3 nor;
    public Vector2 uv;
}

public enum FieldType
{
    Velocity, Pressure, Dye 
}

[System.Serializable]
public class WaterSim
{
    [Space(2)]
    [Header("Compute Shader")]
    [Space(2)]
    public ComputeShader TestShader;
    public ComputeShader LandShader;
    public ComputeShader TerrainShader;
    public ComputeShader WaterShader;
    public ComputeShader StructuredBufferToTextureShader;
    public ComputeShader StructuredBufferUtilityShader;
    public ComputeShader BorderShader;

    [Space(2)]
    [Header("Material")]
    [Space(2)]
    public Material m_WaterMat;

    [Space(2)]
    [Header("Simulation Setting")]
    [Space(1)]
    public float DyeRadius      = 1.0f;
    public float DyeFalloff     = 2.0f;
    public float time_step      = 1;
    public float Viscosity      = 0.5f;

    /**
    * Simulation Buffer
    */
    private CommandBuffer SimulationCommandBuffer;

    /**
    * Mesh Filter
    */
    private MeshFilter[] /*m_LandMeshFilter,*/ m_WaterMeshFilter;
    
    /**
    * Kernel Handle
    */    
    public int _Kernel_Handle_Test_AddColor;
    public int _Kernel_Handle_Test_Diffuse;
    public int _Kernel_Handle_Test_GridColor;
    public int _Kernel_Handle_Test_PipeMethod;


    private int _Kernel_Handle_GenerateMesh;
    private int _Kernel_Handle_SetTerrain_Texture;
    private int _Kernel_Handle_AddColor;
    private int _Kernel_Handle_PipeMethod;
    private int _Kernel_Handle_Dye_st2tx;
    private int _Kernel_Handle_Copy_StructuredBuffer;
    private int _Kernel_Handle_Clear_StructuredBuffer;
    private int _Kernel_Handle_NeuMannBoundary;
    private int _Kernel_Handle_ArbitaryBoundaryVelocity;
    private int _Kernel_Handle_Advection;
    private int _Kernel_Handle_AddForce;
    public int _Kernel_Handle_Pressure_st2tx;
    public int _Kernel_Handle_Velocity_st2tx;
    public int _Kernel_Handle_AddConstantForceSource;
    public int _Kernel_Handle_Divergence;
    /**
    * Mesh
    */
    private Mesh[] m_LandMesh, m_WaterMesh;

    /**
    * Game object
    */
    private GameObject[] /*m_GridLand,*/ m_GridWater;

#region  // Render Texture
    [SerializeField]
    private RenderTexture VisualisationTexture;    
#endregion

#region  //Simulation Setting    
    private const int TERRAIN_LAYERS            = 2;
    private const int TEXTURE_SIZE              = 1024;
    private const int SIMULATION_DIMENSION      = TEXTURE_SIZE / 2;
    private const int TERRAIN_HEIGHT            = 128;
    private const int TOTAL_GRID_SIZE           = 128;
    private const float TIME_STEP               = 0.1f;

    private const int GRID_SIZE                 = 32;
    private const float PIPE_LENGTH             = 1.0f;
    private const float CELL_LENGTH             = 1.0F;
    private const float CELL_AREA               = 1.0f; // CELL_LENGTH * CELL_LENGTH
    private const float GRAVITY                 = 9.81f;
    private const int READ                      = 0;
    private const int WRITE                     = 1;
    private const int RESULT                    = 2;

    private const int GENERATED_VERT_STRIDE     = sizeof(float) * (3 + 3 + 2);
    private const int GENERATED_INDEX_STRIDE    = sizeof(int);
#endregion

    [SerializeField]
    private Vector2 Effect;

    private Camera main_cam;    

    public void Initialize()
    {        
        ComputeShaderUtility.Initialize();

        main_cam = Camera.main;
        if (main_cam == null) Debug.LogError("Could not find main camera, make sure the camera is tagged as main");

        VisualisationTexture                        = new RenderTexture(TEXTURE_SIZE, TEXTURE_SIZE, 0)
        {
            enableRandomWrite = true,
            useMipMap         = false,
        };
        VisualisationTexture.Create();

        // Initialize Kernel Handle
        _Kernel_Handle_Test_GridColor                               = ComputeShaderUtility.GetKernelHandle(TestShader, "TestGridColor");
        _Kernel_Handle_Test_AddColor                                = ComputeShaderUtility.GetKernelHandle(TestShader, "TestAddColor");
        _Kernel_Handle_Test_PipeMethod                              = ComputeShaderUtility.GetKernelHandle(TestShader, "TestPipeMethod");
        _Kernel_Handle_Test_Diffuse                                 = ComputeShaderUtility.GetKernelHandle(TestShader, "TestDifuse");

        _Kernel_Handle_GenerateMesh                                 = ComputeShaderUtility.GetKernelHandle(LandShader, "GenerateMesh");
        _Kernel_Handle_SetTerrain_Texture                           = ComputeShaderUtility.GetKernelHandle(TerrainShader, "SetTerrainTexture");
        _Kernel_Handle_AddColor                                     = ComputeShaderUtility.GetKernelHandle(WaterShader, "AddColor");
        _Kernel_Handle_Advection                                    = ComputeShaderUtility.GetKernelHandle(WaterShader, "Advection");
        _Kernel_Handle_PipeMethod                                   = ComputeShaderUtility.GetKernelHandle(WaterShader, "PipeMethod");
        _Kernel_Handle_Dye_st2tx                                    = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "DyeStructeredToTextureBillinearRGB8");
        _Kernel_Handle_Pressure_st2tx                               = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "PressureStructeredToTextureBillinearR32");
        _Kernel_Handle_Velocity_st2tx                               = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "VelocityStructeredToTextureBillinearRG32");
        _Kernel_Handle_Copy_StructuredBuffer                        = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader, "Copy_StructuredBuffer");
        _Kernel_Handle_Clear_StructuredBuffer                       = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader, "Clear_StructuredBuffer");
        _Kernel_Handle_NeuMannBoundary                              = ComputeShaderUtility.GetKernelHandle(BorderShader, "NeuMannBoundary");
        _Kernel_Handle_ArbitaryBoundaryVelocity                     = ComputeShaderUtility.GetKernelHandle(BorderShader, "ArbitaryBoundaryVelocity");
        _Kernel_Handle_Divergence                                   = ComputeShaderUtility.GetKernelHandle(WaterShader, "Divergence");

        _Kernel_Handle_AddForce                                     = ComputeShaderUtility.GetKernelHandle(WaterShader, "AddForce");
        _Kernel_Handle_AddConstantForceSource                       = ComputeShaderUtility.GetKernelHandle(WaterShader, "AddConstantForceAt");

        StructuredBufferToTextureShader.SetInt("_Dye_Results_Resolution", TEXTURE_SIZE); 

        SimulationCommandBuffer = new CommandBuffer()
        {
            name = "Simulation_Command_Buffer"
        };
        SimulationCommandBuffer.SetGlobalInt    (   "_texture_size",    SIMULATION_DIMENSION         );
        SimulationCommandBuffer.SetGlobalFloat  (   "_time_step",       TIME_STEP                    );
        SimulationCommandBuffer.SetGlobalFloat  (   "_grid_size",       GRID_SIZE                    );

        // Init Water Data
        // WaterData = new WaterData(WaterShader, _Kernel_Handle_PipeMethod, TEXTURE_SIZE);

        MakeGrids();        
    }
//////////////////////////////////////
    public void TestGridColor(ComputeBuffer buffer)
    {
        float randomHue = Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f) + Mathf.Sin(Time.time * 0.7f + 2.0f));
        randomHue = randomHue - Mathf.Floor(randomHue);

        TestShader.SetInt("_grid_size_", 4);
        TestShader.SetInt("_grid_scale", SIMULATION_DIMENSION / 4);
        TestShader.SetVector("_test_grid_color", Color.HSVToRGB(randomHue, Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f))*0.2f + 0.8f, Mathf.Abs(Mathf.Sin(Time.time * 0.7f + 2.0f)) * 0.2f + 0.5f));
        SetBufferOnCommandList(SimulationCommandBuffer, buffer, "_test_buffer_color");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            TestShader,
            _Kernel_Handle_Test_GridColor,
            SIMULATION_DIMENSION,
            SIMULATION_DIMENSION,
            1
        );
    }

    public void TestAddColor(ComputeBuffer buffer)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, buffer, "_test_result_buffer_AddColor");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            TestShader,
            _Kernel_Handle_Test_AddColor,
            SIMULATION_DIMENSION,
            SIMULATION_DIMENSION,
            1
        );
    }

    public void TestPipeMethod(ComputeBuffer Buffer)
    {
        TestShader.SetFloat(    "_cross_section",   (float) (3.14 * Mathf.Pow((127 / 2), 2))        );
        TestShader.SetFloat(    "_gravity",         9.81f                                        );
        TestShader.SetFloat(    "_length",          127f                                            );
        TestShader.SetFloat(    "_time_step_",      1f                                              );


        // SetBufferOnCommandList(SimulationCommandBuffer, Buffer, "_test_buffer_color_");
        // SetBufferOnCommandList(SimulationCommandBuffer, Buffer, "_test_update_buffer_color_");
        // SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_test_Result_buffer_PipeMethod");
        // DispatchComputeOnCommandBuffer(
        //     SimulationCommandBuffer, 
        //     TestShader, 
        //     _Kernel_Handle_Test_PipeMethod, 
        //     SIMULATION_DIMENSION, 
        //     SIMULATION_DIMENSION, 
        //     1
        // );        

        // SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,       "_Copy_Source"              );
        // SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                             "_Copy_Target"              );
        // DispatchComputeOnCommandBuffer(
        //     SimulationCommandBuffer, 
        //     StructuredBufferUtilityShader, 
        //     _Kernel_Handle_Copy_StructuredBuffer, 
        //     SIMULATION_DIMENSION * SIMULATION_DIMENSION, 
        //     1, 
        //     1
        // );

        // ClearBuffer(WaterSimResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        bool ping_as_results = false;
        for (int i = 0; i < 80; ++i)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results)
            {
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer, "_test_buffer_color_");
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer, "_test_update_buffer_color_");
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_test_Result_buffer_PipeMethod");
            }
            else
            {           
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_test_buffer_color_");
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_test_update_buffer_color_");
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer, "_test_Result_buffer_PipeMethod");
            }
            DispatchComputeOnCommandBuffer(
                SimulationCommandBuffer, 
                TestShader, 
                _Kernel_Handle_Test_PipeMethod, 
                SIMULATION_DIMENSION, 
                SIMULATION_DIMENSION, 
                1
            );        

        }

        if (ping_as_results)                         // The Ping ponging ended on the helper buffer ping. Copy it to the buffer_to_diffuse buffer
        {
            Debug.Log("Diffuse Ended on a Ping Target, now copying over the Ping to the buffer which was supposed to be diffused");

            SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,       "_Copy_Source"              );
            SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                        "_Copy_Target"              );
            DispatchComputeOnCommandBuffer(
                SimulationCommandBuffer, 
                StructuredBufferUtilityShader, 
                _Kernel_Handle_Copy_StructuredBuffer, 
                SIMULATION_DIMENSION * SIMULATION_DIMENSION, 
                1, 
                1
            );
        }

        ClearBuffer(WaterSimResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }








    public void AddForce(ComputeBuffer ForceBuffer)
    {
        WaterShader.SetVector("_pos_current", new Vector2 (260f, 260f));
        WaterShader.SetVector("_pos_prev", new Vector2 (250f, 250f));
        
        SetBufferOnCommandList(SimulationCommandBuffer, ForceBuffer, "_user_applied_force_buffer");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            WaterShader,
            _Kernel_Handle_AddForce,
            SIMULATION_DIMENSION,
            SIMULATION_DIMENSION,
            1
        );
    }

    public void AddConstantForceSource (
        ComputeBuffer ForceBuffer,
        Vector2 SourcePosition,
        Vector2 ForceDirection,
        float ForceStrength,
        float SourceRadius,
        float SourceFalloff
    )
    {
        ForceDirection.Normalize();

        SimulationCommandBuffer.SetComputeFloatParam(WaterShader, "_constant_force_radius", SourceRadius);
        SimulationCommandBuffer.SetComputeFloatParam(WaterShader, "_constant_force_falloff", SourceFalloff);
        SimulationCommandBuffer.SetComputeVectorParam(WaterShader, "_constant_force_source_position", SourcePosition);
        SimulationCommandBuffer.SetComputeVectorParam(WaterShader, "_constant_force_source_direction", ForceDirection * ForceStrength);

        SetBufferOnCommandList(SimulationCommandBuffer, ForceBuffer, "_user_applied_force_buffer");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            WaterShader,
            _Kernel_Handle_AddConstantForceSource,
            SIMULATION_DIMENSION,
            SIMULATION_DIMENSION,
            1
        );
    }

//////////////////////////////////////
    public void AddDye(ComputeBuffer DyeBuffer)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, DyeBuffer, "_water_color_buffer");
        
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            WaterShader, 
            _Kernel_Handle_AddColor, 
            SIMULATION_DIMENSION, 
            SIMULATION_DIMENSION, 
            1
        );
    }

    public void Diffuse(ComputeBuffer Buffer)
    {
               float centerFactor              = 1.0f / (Viscosity * time_step);
        float reciprocal_of_diagonal    = (Viscosity * time_step) / (1.0f + 4.0f * (Viscosity * time_step));

        SimulationCommandBuffer.SetGlobalFloat      (       "A",                    1f                      );
        SimulationCommandBuffer.SetGlobalFloat      (       "g",                    9.18f                   );
        SimulationCommandBuffer.SetGlobalFloat      (       "L",                    1f                      );

        SimulationCommandBuffer.SetGlobalFloat      (       "_centerFactor",        centerFactor            );
        SimulationCommandBuffer.SetGlobalFloat      (       "_rDiagonal",           reciprocal_of_diagonal  );

        bool ping_as_results = false;
        for (int i = 0; i < 80; ++i)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results)
            {
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                    "_current_buffer"           );
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                    "_update_water_buffer"      );
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,   "_water_buffer"             );        
            }
            else
            {
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,   "_current_buffer"           );
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,   "_update_water_buffer"      );
                SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                    "_water_buffer"             );        
            }
            
            DispatchComputeOnCommandBuffer(
                SimulationCommandBuffer, 
                WaterShader, 
                _Kernel_Handle_PipeMethod, 
                SIMULATION_DIMENSION, 
                SIMULATION_DIMENSION, 
                1
            );
        }

        if (ping_as_results)                         // The Ping ponging ended on the helper buffer ping. Copy it to the buffer_to_diffuse buffer
        {
            Debug.Log("Diffuse Ended on a Ping Target, now copying over the Ping to the buffer which was supposed to be diffused");

            SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,       "_Copy_Source"              );
            SetBufferOnCommandList(SimulationCommandBuffer, Buffer,                        "_Copy_Target"              );
            DispatchComputeOnCommandBuffer(
                SimulationCommandBuffer, 
                StructuredBufferUtilityShader, 
                _Kernel_Handle_Copy_StructuredBuffer, 
                SIMULATION_DIMENSION * SIMULATION_DIMENSION, 
                1, 
                1
            );
        }

        ClearBuffer(WaterSimResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }

    public void Advection(ComputeBuffer WaterBuffer, ComputeBuffer VelocityBuffer, float DissipationFactor)
    {
        SimulationCommandBuffer.SetGlobalFloat("_dissipationFactor", DissipationFactor);

        SetBufferOnCommandList(SimulationCommandBuffer, VelocityBuffer,                 "_velocity_field_buffer"    );
        SetBufferOnCommandList(SimulationCommandBuffer, WaterBuffer,                    "_field_to_advect_buffer"   );
        SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,   "_new_advected_field"       );

        
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            WaterShader, 
            _Kernel_Handle_Advection, 
            SIMULATION_DIMENSION, 
            SIMULATION_DIMENSION, 
            1
        );
        
        SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing,   "_Copy_Source");
        SetBufferOnCommandList(SimulationCommandBuffer, WaterBuffer,                    "_Copy_Target");

        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            StructuredBufferUtilityShader, 
            _Kernel_Handle_Copy_StructuredBuffer, 
            SIMULATION_DIMENSION * SIMULATION_DIMENSION, 
            1, 
            1
        );

        // -------------
        ClearBuffer(WaterSimResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));        
    }

    public void HandleCornerBoundaries(ComputeBuffer SetBoundary, FieldType FieldType)
    {
        float scale;
        switch (FieldType)
        {
            case FieldType.Dye:      scale =  0.0f; break;
            case FieldType.Velocity: scale = -1.0f; break;
            case FieldType.Pressure: scale =  1.0f; break;
            default:                 scale =  0.0f; break;
        }

        SimulationCommandBuffer.SetGlobalFloat(                         "_neumaboundary_scale", scale       );
        SetBufferOnCommandList(SimulationCommandBuffer, SetBoundary,    "_neumaboundary_field_to_contain"   );
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            BorderShader, 
            _Kernel_Handle_NeuMannBoundary, 
            SIMULATION_DIMENSION * 4, 
            1, 
            1
        );
    }

    public void HandleArbitaryBoundery(ComputeBuffer SetBoundaryOn, ComputeBuffer OffsetBuffer)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, SetBoundaryOn, "_velocity_buffer");
        SetBufferOnCommandList(SimulationCommandBuffer, OffsetBuffer, "_perCellArbitaryBoundryOffsetsVellocity");
        SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_new_handleded_velocity");

        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            BorderShader,
            _Kernel_Handle_ArbitaryBoundaryVelocity,
            SIMULATION_DIMENSION,
            SIMULATION_DIMENSION,
            1
        );
    }

    public void SetTextureToMaterial(Texture Texture, Material Material)
    {
        Material.SetTexture("_MainText", Texture);
    }

    public void Visualiuse (ComputeBuffer BufferToVisualize)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, BufferToVisualize,      "_Dye_StructuredToTexture_Source_RBB8");
        StructuredBufferToTextureShader.SetTexture(_Kernel_Handle_Dye_st2tx,    "_Dye_StructuredToTexture_Results_RBB8", VisualisationTexture);

        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            StructuredBufferToTextureShader, 
            _Kernel_Handle_Dye_st2tx, 
            TEXTURE_SIZE, 
            TEXTURE_SIZE, 
            1
        );

        SimulationCommandBuffer.Blit(VisualisationTexture, BuiltinRenderTextureType.CameraTarget);
    }

    public void Visualiuse (ComputeBuffer BufferToVisualize, Texture Texture)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, BufferToVisualize,      "_Dye_StructuredToTexture_Source_RBB8");
        StructuredBufferToTextureShader.SetTexture(_Kernel_Handle_Dye_st2tx,    "_Dye_StructuredToTexture_Results_RBB8", Texture);

        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            StructuredBufferToTextureShader, 
            _Kernel_Handle_Dye_st2tx, 
            TEXTURE_SIZE, 
            TEXTURE_SIZE, 
            1
        );

        SimulationCommandBuffer.Blit(Texture, BuiltinRenderTextureType.CameraTarget);
    }

    public void CopyPressureBufferToTexture(RenderTexture Texture, ComputeBuffer BufferToVisualize)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, BufferToVisualize, "_Pressure_StructeredToTexture_Source_R32");
        StructuredBufferToTextureShader.SetTexture(_Kernel_Handle_Pressure_st2tx, "_Pressure_StructeredToTexture_Results_R32", Texture);
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            StructuredBufferToTextureShader,
            _Kernel_Handle_Pressure_st2tx,
            TEXTURE_SIZE,
            TEXTURE_SIZE,
            1
        );
    }

    public void CopyVelocityBufferToTexture(RenderTexture Texture, ComputeBuffer BufferToVisualize)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, BufferToVisualize, "_Velocity_StructeredToTexture_Source_RB32");
        StructuredBufferToTextureShader.SetTexture(_Kernel_Handle_Velocity_st2tx, "_Velocity_StructeredToTexture_Results_RB32", Texture);
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer,
            StructuredBufferToTextureShader,
            _Kernel_Handle_Velocity_st2tx,
            TEXTURE_SIZE,
            TEXTURE_SIZE,
            1
        );
    }

    public void Project(
        ComputeBuffer BufferToMakeDivergenceFree, 
        ComputeBuffer DivergenceField, 
        ComputeBuffer PressureField, 
        ComputeBuffer BoundaryPressureOffsetBuffer
    )
    {
        CalculateFieldDivergence(BufferToMakeDivergenceFree, DivergenceField);

        float centerFactor = -1.0f * GRID_SIZE * GRID_SIZE;
        float diagonalFactor = 0.25f;

        SimulationCommandBuffer.SetGlobalFloat("_centerFactor", centerFactor);
        SimulationCommandBuffer.SetGlobalFloat("_rDiagonal", diagonalFactor);

        SetBufferOnCommandList(SimulationCommandBuffer, DivergenceField, "_b_buffer");

        ClearBuffer(PressureField, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        bool ping_as_results = false;

        for (int i = 0; i < 80; i++)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results)                     // Ping ponging back and forth to insure no racing condition. 
            {
                HandleCornerBoundaries(PressureField, FieldType.Pressure);
                SetBufferOnCommandList(SimulationCommandBuffer, PressureField, "_updated_x_buffer");
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_results");
            }
            else
            {
                HandleCornerBoundaries(WaterSimResources.BufferPing, FieldType.Pressure);
                SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_updated_x_buffer");
                SetBufferOnCommandList(SimulationCommandBuffer, PressureField, "_results");
            }

            SimulationCommandBuffer.SetGlobalInt("_current_iteration", i);
            DispatchComputeOnCommandBuffer(SimulationCommandBuffer, WaterShader, _Kernel_Handle_PipeMethod, SIMULATION_DIMENSION, SIMULATION_DIMENSION, 1);
        }

        if (ping_as_results)
        {
            SetBufferOnCommandList(SimulationCommandBuffer, WaterSimResources.BufferPing, "_Copy_Source");
            SetBufferOnCommandList(SimulationCommandBuffer, PressureField, "_Copy_Target");
            DispatchComputeOnCommandBuffer(SimulationCommandBuffer, StructuredBufferUtilityShader, _Kernel_Handle_Copy_StructuredBuffer, SIMULATION_DIMENSION * SIMULATION_DIMENSION, 1, 1);
        }

        ClearBuffer(WaterSimResources.BufferPing, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        // HandleCornerBoundaries(PressureField, FieldType.Pressure);

        // CalculateDivergenceFreeFromPressureField(buffer_to_make_divergence_free, PressureField, FluidGPUResources.buffer_pong, FluidGPUResources.buffer_ping);

        // SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_Copy_Source");
        // SetBufferOnCommandList(sim_command_buffer, buffer_to_make_divergence_free, "_Copy_Target");
        // DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, _handle_Copy_StructuredBuffer, simulation_dimension * simulation_dimension, 1, 1);

        // ClearBuffer(FluidGPUResources.buffer_ping, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        // ClearBuffer(FluidGPUResources.buffer_pong, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }

    private void CalculateFieldDivergence(ComputeBuffer FieldToCalculate, ComputeBuffer DivergenceBuffer)
    {
        SetBufferOnCommandList(SimulationCommandBuffer, FieldToCalculate, "_divergence_vector_field");
        SetBufferOnCommandList(SimulationCommandBuffer, DivergenceBuffer, "_divergence_values");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            WaterShader, 
            _Kernel_Handle_Divergence, 
            SIMULATION_DIMENSION, 
            SIMULATION_DIMENSION, 
            1
        );
    }

    private void ClearBuffer(ComputeBuffer buffer, Vector4 clear_value)
    {
        SimulationCommandBuffer.SetGlobalVector("_Clear_Value_StructuredBuffer", new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        SetBufferOnCommandList(SimulationCommandBuffer, buffer, "_Clear_Target_StructuredBuffer");
        DispatchComputeOnCommandBuffer(
            SimulationCommandBuffer, 
            StructuredBufferUtilityShader, 
            _Kernel_Handle_Clear_StructuredBuffer, 
            SIMULATION_DIMENSION * SIMULATION_DIMENSION, 
            1, 
            1
        );
    }

    /**
    * Tick
    */
    public void Tick()
    {
        // WaterShader.SetFloat("_time", Time.time);

        int forceController = 0;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SimulationCommandBuffer.SetGlobalInt("_current_iteration", 1);
            float randomHue = Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f) + Mathf.Sin(Time.time * 0.7f + 2.0f));
            randomHue = randomHue - Mathf.Floor(randomHue);
            // SetBufferOnCommandList(SimulationCommandBuffer, buffer, "_test_buffer_AddColor");
            TestShader.SetVector("_test_color", Color.HSVToRGB(randomHue, Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f))*0.2f + 0.8f, Mathf.Abs(Mathf.Sin(Time.time * 0.7f + 2.0f)) * 0.2f + 0.5f));
            forceController = 1;
        }
        else
        {
            SimulationCommandBuffer.SetGlobalInt("_current_iteration", 0);      
        }

        WaterShader.SetFloat ("_force_multiplier",      forceController     );
        WaterShader.SetFloat ("_force_effect_radius",   2                   );
        WaterShader.SetFloat ("_force_falloff",         1                   );
    }

    public int GetSimulationDimension()
    {
        return SIMULATION_DIMENSION;
    }

    public void BindComputeBuffer()
    {
        main_cam.AddCommandBuffer(CameraEvent.AfterEverything, SimulationCommandBuffer);        
    }

    public void Release()
    {
        VisualisationTexture.Release();
        ComputeShaderUtility.Release();
    }






    private void SetBufferOnCommandList(CommandBuffer cb, ComputeBuffer buffer, string buffer_name)
    {
        cb.SetGlobalBuffer(buffer_name, buffer);
    }

    private void DispatchComputeOnCommandBuffer(
        CommandBuffer cb, 
        ComputeShader toDispatch, 
        int kernel, 
        uint thread_num_x, 
        uint thread_num_y, 
        uint thread_num_z
    )
    {
        DispatchDimensions group_nums = ComputeShaderUtility.CheckGetDispatchDimensions(
            toDispatch, 
            kernel, 
            thread_num_x, 
            thread_num_y, 
            thread_num_z
        );

        cb.DispatchCompute(
            toDispatch, kernel, 
            (int) group_nums.dispatch_x, 
            (int) group_nums.dispatch_y, 
            (int) group_nums.dispatch_z
        );

        // Debug
        Debug.Log(string.Format("Attached the computeshader {0}, at kernel {1}, to the commandbuffer {2}." +
            "Dispatch group numbers are, in x, y,z respectivly: {3}", 
            toDispatch.name, ComputeShaderUtility.GetKernelNameFromHandle(toDispatch, kernel), cb.name,
            group_nums.ToString()));
    }

#region  // Make Grids
    /**
    * Make Grids
    */
    private void MakeGrids()
    {
        int numGrids = 1;

        // m_GridLand                          = new GameObject[numGrids * numGrids];
        m_GridWater                         = new GameObject[numGrids * numGrids];

        for (int x = 0; x < numGrids; x++)
        {
            for (int y = 0; y < numGrids; y++)
            {
                int idx         = x + y * numGrids;

                int posX        = x * (GRID_SIZE - 1);
                int posY        = y * (GRID_SIZE - 1);

                Mesh mesh       = MakeMesh(GRID_SIZE, TOTAL_GRID_SIZE, posX, posY);

                mesh.bounds     = new Bounds(new Vector3(GRID_SIZE / 2, 0, GRID_SIZE / 2), new Vector3(GRID_SIZE, TERRAIN_HEIGHT * 2, GRID_SIZE));

                // m_GridLand[idx]                                     = new GameObject("Grid Land " + idx.ToString());
                // m_GridLand[idx].AddComponent<MeshFilter>();
                // m_GridLand[idx].AddComponent<MeshRenderer>();
                // m_GridLand[idx].GetComponent<Renderer>().material   = m_LandMat;
                // m_GridLand[idx].GetComponent<MeshFilter>().mesh     = mesh;
                // m_GridLand[idx].transform.localPosition             = new Vector3(-TOTAL_GRID_SIZE / 2 + posX, 0, -TOTAL_GRID_SIZE / 2 + posY);

                m_GridWater[idx]                                    = new GameObject("Grid Water " + idx.ToString());
                m_GridWater[idx].AddComponent<MeshFilter>();
                m_GridWater[idx].AddComponent<MeshRenderer>();
                m_GridWater[idx].GetComponent<Renderer>().material  = m_WaterMat; // Set Default material
                m_GridWater[idx].GetComponent<MeshFilter>().mesh    = mesh;
                m_GridWater[idx].transform.localPosition            = new Vector3(-TOTAL_GRID_SIZE / 2 + posX, 0, -TOTAL_GRID_SIZE / 2 + posY);

            }
        }
    }

    /**
    * Make Mesh
    */
    private Mesh MakeMesh(int Size, int TotalSize, int PosX, int PosY)
    {
        VertexData[] generatedVertices              = new VertexData[GRID_SIZE * GRID_SIZE];
        int[] generatedIndices                      = new int[GRID_SIZE * GRID_SIZE * 6];

        //compute shader
        // _kernel = LandShader.FindKernel ("GenerateMesh");
        GraphicsBuffer GenerateVertexBuffer         = new GraphicsBuffer(GraphicsBuffer.Target.Structured, generatedVertices.Length, GENERATED_VERT_STRIDE);
        GraphicsBuffer GenerateIndicesBuffer        = new GraphicsBuffer(GraphicsBuffer.Target.Structured, generatedIndices.Length, GENERATED_INDEX_STRIDE);

        LandShader.SetBuffer(_Kernel_Handle_GenerateMesh, "_generated_vertices", GenerateVertexBuffer);
        LandShader.SetBuffer(_Kernel_Handle_GenerateMesh, "_generated_indices", GenerateIndicesBuffer);
        LandShader.SetInt("_grid_Size", GRID_SIZE);
        LandShader.SetInt("_total_grid_size", GRID_SIZE);
        LandShader.SetInt("PosX", PosX);
        LandShader.SetInt("PosY", PosY);

       LandShader.GetKernelThreadGroupSizes(_Kernel_Handle_GenerateMesh, out uint threadGroupSize, out _, out _);
        int dispatchSize = Mathf.CeilToInt((float)GRID_SIZE / threadGroupSize);
        // Dispatch the compute LandShader
        LandShader.Dispatch(_Kernel_Handle_GenerateMesh, dispatchSize, 1, 1);

        GenerateVertexBuffer.GetData(generatedVertices);
        GenerateIndicesBuffer.GetData(generatedIndices);

        Mesh mesh                   = new Mesh();
        Vector3[] vertices          = new Vector3[generatedVertices.Length];
        Vector3[] normals           = new Vector3[generatedVertices.Length];
        Vector2[] uvs               = new Vector2[generatedVertices.Length];
        for(int i = 0; i < generatedVertices.Length; i++) {
            var v           = generatedVertices[i];            
            vertices[i]     = v.pos;
            normals[i]      = v.nor;
            uvs[i]          = v.uv;
        }

        mesh.vertices   = vertices;
        mesh.uv         = uvs;
        mesh.triangles  = generatedIndices;
        mesh.normals    = normals;

        return mesh;
    }
#endregion
}
