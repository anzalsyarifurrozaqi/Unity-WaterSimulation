using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

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
    public ComputeShader WaterShader;
    
    
    
    public Material m_WaterMaterial;


    /**
    * Simulation Buffer
    */
    private CommandBuffer SimulationCommandBuffer;

    /**
    * Mesh Filter
    */
    private MeshFilter m_WaterMeshFilter;
    
    /**
    * Kernel Handle
    */    
    public int _Kernel_Handle_GenerateMesh;
    public int _Kernel_Handle_Test_SurfacePos;
    /**
    * Mesh
    */
    private Mesh m_WaterMesh;

    /**
    * Game object
    */
    private GameObject m_GridWater;
    private const int SURFACE_SIZE      = 1024;
    private const int TERRAIN_HEIGHT    = 128;
    private const int TOTAL_GRID_SIZE   = 128;
    private const int GRID_SIZE         = 32;

    private const int GENERATED_VERT_STRIDE     = sizeof(float) * (3 + 3 + 2);
    private const int GENERATED_INDEX_STRIDE    = sizeof(int);

    private NativeArray<VertexData> vertData;
    private AsyncGPUReadbackRequest request;
    public void Initialize()
    {        
        ComputeShaderUtility.Initialize();

        // Initialize Kernel Handle
        _Kernel_Handle_GenerateMesh                                   = ComputeShaderUtility.GetKernelHandle(WaterShader, "GenerateMesh");
        _Kernel_Handle_Test_SurfacePos                                = ComputeShaderUtility.GetKernelHandle(TestShader, "TestSurfacePos");

        SimulationCommandBuffer = new CommandBuffer()
        {
            name = "Simulation_Command_Buffer"
        };
        SimulationCommandBuffer.SetGlobalInt    (   "_grid_size",    GRID_SIZE            );        

        // Init Water Data
        // WaterData = new WaterData(WaterShader, _Kernel_Handle_PipeMethod, TEXTURE_SIZE);

        MakeGrids();        

        // Init mesh vertex array
        vertData = new NativeArray<VertexData>(m_WaterMesh.vertexCount, Allocator.Temp);
        for (int i = 0; i < m_WaterMesh.vertexCount; ++i)
        {
            VertexData v = new VertexData();
            v.pos = m_WaterMesh.vertices[i];
            v.nor = m_WaterMesh.normals[i];
            v.uv = m_WaterMesh.uv[i];
            vertData[i] = v;
        }

        Debug.Log(m_WaterMesh.vertexCount);

        //Using 2019.3 new Mesh API
        var layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, m_WaterMesh.GetVertexAttributeFormat(VertexAttribute.Position), 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, m_WaterMesh.GetVertexAttributeFormat(VertexAttribute.Normal), 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, m_WaterMesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0), 2),
        };
        m_WaterMesh.SetVertexBufferParams(m_WaterMesh.vertexCount, layout);
    }

    public void TestSurfacePos(ComputeBuffer Buffer)
    {        
        if(vertData.IsCreated) Buffer.SetData(vertData);
        //Request AsyncReadback

        TestShader.SetBuffer(_Kernel_Handle_Test_SurfacePos, "_test_buffer_surface", Buffer);

        request = AsyncGPUReadback.Request(Buffer);

        TestShader.Dispatch(_Kernel_Handle_Test_SurfacePos, GRID_SIZE / 16, GRID_SIZE / 16, 1);
    }

    /**
    * Tick
    */
    public void Tick(ComputeBuffer Buffer)
    {
        if(request.done && !request.hasError)
        {
            //Readback and show result on texture
            vertData = request.GetData<VertexData>();

            //Update mesh
            m_WaterMesh.MarkDynamic();
            m_WaterMesh.SetVertexBufferData(vertData,0,0,vertData.Length);
            m_WaterMesh.RecalculateNormals();

			Debug.Log("vert 0" + vertData[0].pos);
			Debug.Log("vert 1" + vertData[1].pos);

            //Request AsyncReadback again
            request = AsyncGPUReadback.Request(Buffer);
        }
    }

    public int GetSimulationDimension()
    {
        return SURFACE_SIZE;
    }

    public void Release()
    {        
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
        m_GridWater                         = new GameObject();        

        int posX        = (GRID_SIZE - 1);
        int posY        = (GRID_SIZE - 1);

        Mesh mesh       = MakeMesh(GRID_SIZE, TOTAL_GRID_SIZE, posX, posY);

        mesh.bounds     = new Bounds(new Vector3(GRID_SIZE / 2, 0, GRID_SIZE / 2), new Vector3(GRID_SIZE, TERRAIN_HEIGHT * 2, GRID_SIZE));

        m_GridWater                                    = new GameObject("Grid Water");
        m_GridWater.AddComponent<MeshFilter>();
        m_GridWater.AddComponent<MeshRenderer>();                
        m_GridWater.GetComponent<Renderer>().material = m_WaterMaterial;
        m_GridWater.GetComponent<MeshFilter>().mesh    = mesh;
        m_GridWater.transform.localPosition            = new Vector3(-TOTAL_GRID_SIZE / 2 + posX, 0, -TOTAL_GRID_SIZE / 2 + posY);

        m_WaterMesh = mesh;
        m_WaterMeshFilter = m_GridWater.GetComponent<MeshFilter>();
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

        WaterShader.SetBuffer(_Kernel_Handle_GenerateMesh, "_generated_vertices",   GenerateVertexBuffer    );
        WaterShader.SetBuffer(_Kernel_Handle_GenerateMesh, "_generated_indices",    GenerateIndicesBuffer   );

        WaterShader.SetInt(     "_grid_Size",       GRID_SIZE   );
        WaterShader.SetInt(     "_total_grid_size", GRID_SIZE   );
        WaterShader.SetInt(     "PosX",             PosX        );
        WaterShader.SetInt(     "PosY",             PosY        );

       WaterShader.GetKernelThreadGroupSizes(_Kernel_Handle_GenerateMesh, out uint threadGroupSize, out _, out _);
        int dispatchSize = Mathf.CeilToInt((float) GRID_SIZE / threadGroupSize + 1);
        // Dispatch the compute WaterShader
        WaterShader.Dispatch(_Kernel_Handle_GenerateMesh, dispatchSize, 1, 1);

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
