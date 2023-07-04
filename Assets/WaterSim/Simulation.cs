using System;
using UnityEngine;
using Assets.Scripts.Utils;
using UnityEngine.Rendering.Universal;

public class Simulation : MonoBehaviour
{
    [Header("References")]
    public Material[] Materials;
    
    public ComputeShader ErosionComputeShader;

    [Header("Drawing")]
    public float BrushAmount = 0f;

    [Serializable]
    public class SimulationSettings
    {
        [Range(0f, 10f)]
        public float TimeScale = 1f;

        [Range(0, 0.5f)]
        public float RainRate = 0.012f;

        [Range(0.1f, 20f)]
        public float Gravity = 9.81f;

        [Range(0.001f, 1000f)]
        public float PipeArea = 20;

        public float PipeLength = 1f / 256f;

        public Vector2 CellSize = new Vector2(1f / 256f, 1f / 256f);

        [Range(0.1f, 1f)]
        public float Amplitude = 0.1f;

        [Range(0f, 360f)]
        public float Direction = 0.0f;

        [Range(1f, 45f)]
        public float WaveLength = 0.0f;

        [Range(1, 8)]
        public int WaveCount = 1;

        [Range(1f, 20f)]
        public float WaterHeight = 1f;
    }

    [Header("Simulation Settings")]
    [Range(32, 1024)]
    public int Width = 256;
    [Range(32, 1024)]
    public int Height = 256;

    public LayerMask BuoyancyMask;
    public SimulationSettings Settings;

    // State texture ARGBFloat
    // R - Surface height [0, +inf]
    // G - Water over surface height [0, +inf]
    // B - Suspended sediment amount [0, +inf]
    // A - Hardness of the surface [0, 1]

    private RenderTexture _StateTexture;
    private RenderTexture _StateTextureBuffer;

    // Output water flux-field texture
    // represent how much water is OUTGOING in each direction
    // R - flux to the left cell [0, +inf]
    // G - flux to the left cell [0, +inf]
    // B - flux to the left cell [0, +inf]
    // A - flux to the left cell [0, +inf]
    private RenderTexture _WaterFluxTexture;

    // Velocity texture
    // R - X-velocity [-inf, +inf]
    // G - Y-velocity [-inf, +inf]
    private RenderTexture _VelocityTexture;

    [SerializeField]
    private RenderTexture _DepthTexture;

    [SerializeField]
    private RenderTexture _WaveTexture;

    // List of Kernels in the compute shader to be dispatched
    // Sequentially in this order

    private readonly string[] _KernelNames =
    {
        "PressureControl",
        "FluxComputation",
        "FluxApply",
        "GerstnerWave"
    };

    // Kernel-related data
    private int[] _Kernels;
    private uint _ThreadsPerGroupX;
    private uint _ThreadsPerGroupY;
    private uint _ThreadsPerGroupZ;

    // Rendering stuff
    private const string StateTextureKey = "_StateTex";
    private const string WaveTextureKey = "_WaveTex";

    // Brush
    private Plane _floor = new Plane(Vector3.up, Vector3.zero);
    private float _brushRadius = 0.01f;
    private Vector4 _inputControls;
    private Material _copyMat;

    // Camera
    private Camera _DepthCam;

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;


        Initialize();
    }

    void Update()
    {
        // Controls
        _brushRadius = Mathf.Clamp(_brushRadius + Input.mouseScrollDelta.y * Time.deltaTime * 0.2f, 0.01f, 1f);

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var amount = 0f;
        var brushX = 0f;
        var brushY = 0f;

        if (_floor.Raycast(ray, out var enter))
        {
            var hitPoint = ray.GetPoint(enter);
            brushX = hitPoint.x / Width;
            brushY = hitPoint.z / Height;

            if (Input.GetMouseButton(0))
            {
                amount = BrushAmount;                       
            }
        }
        else
        {
            amount = 0f;
        }
        
        _inputControls = new Vector4(brushX, brushY, _brushRadius, amount);
        Shader.SetGlobalVector("_InputControls", _inputControls);
    }

    void FixedUpdate()
    {
        // Compute dispatch
        if (ErosionComputeShader != null)
        {
            if (Settings != null)
            {
                // General parameters
                ErosionComputeShader.SetFloat   (   "_TimeDelta",   Time.fixedDeltaTime * Settings.TimeScale    );
                ErosionComputeShader.SetFloat   (   "_RainState",   Settings.RainRate                           );
                ErosionComputeShader.SetFloat   (   "_Gravity",     Settings.Gravity                            );
                ErosionComputeShader.SetFloat   (   "_PipeArea",    Settings.PipeArea                           );
                ErosionComputeShader.SetFloat   (   "_PipeLength",  Settings.PipeLength                         );
                ErosionComputeShader.SetVector  (   "_CellSize",    Settings.CellSize                           );


                // Wave System
                ErosionComputeShader.SetFloat   (   "_Time",        Time.time           );
                ErosionComputeShader.SetFloat   (   "_Amplitude",   Settings.Amplitude  );
                ErosionComputeShader.SetFloat   (   "_Direction",   Settings.Direction  );
                ErosionComputeShader.SetFloat   (   "_WaveLength",  Settings.WaveLength );
                ErosionComputeShader.SetInt     (   "_WaveCount",   Settings.WaveCount  );


                // Inputs
                ErosionComputeShader.SetVector("_InputControls", _inputControls);
            }
        }

        // Dispatch all passes sequentially
        foreach (var Kernel in _Kernels)
        {
            ErosionComputeShader.Dispatch(
                Kernel,
                _StateTexture.width     / (int) _ThreadsPerGroupX,
                _StateTexture.height    / (int) _ThreadsPerGroupY,
                1
            );
        }
    }

    [ContextMenu("Initialize")]
    void Initialize()
    {
        /* ====== Setup Computation ====== */
        // If therea re already existing textures - release them
        if (_StateTexture != null)
        {
            _StateTexture.Release();
        }

        if (_WaterFluxTexture != null)
        {
            _WaterFluxTexture.Release();
        }

        if (_VelocityTexture != null)
        {
            _VelocityTexture.Release();
        }

        if (_DepthTexture != null)
        {
            _DepthTexture.Release();
        }

        // Initialize texture for storing height map
        _StateTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite   = true,
            filterMode          = FilterMode.Bilinear,
            wrapMode            = TextureWrapMode.Clamp
        };  

        _WaterFluxTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite   = true,
            filterMode          = FilterMode.Bilinear,
            wrapMode            = TextureWrapMode.Clamp
        };

        _VelocityTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.RGFloat)
        {
            enableRandomWrite   = true,
            filterMode          = FilterMode.Point,
            wrapMode            = TextureWrapMode.Clamp
        };

        _DepthTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.RGFloat)
        {
            // enableRandomWrite   = true,
            filterMode          = FilterMode.Bilinear,
            wrapMode            = TextureWrapMode.Clamp,
        };

        _WaveTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.RGFloat)
        {
            enableRandomWrite           = true,
            filterMode                  = FilterMode.Bilinear,
            wrapMode                    = TextureWrapMode.Clamp
        };


        CaptureDepthMap();

        if (!_StateTexture.IsCreated())
        {
            _StateTexture.Create();
        }

        if (ErosionComputeShader != null)
        {
            _Kernels    = new int[_KernelNames.Length];
            var i       = 0;
            foreach (var kernelName in _KernelNames)
            {
                var kernel = ErosionComputeShader.FindKernel(kernelName);
                _Kernels[i++] = kernel;

                // Set all textures
                ErosionComputeShader.SetTexture(kernel, "HeightMap",     _StateTexture      );
                ErosionComputeShader.SetTexture(kernel, "FluxMap",      _WaterFluxTexture   );
                ErosionComputeShader.SetTexture(kernel, "VelocityMap",  _VelocityTexture    );
                ErosionComputeShader.SetTexture(kernel, "WaveMap",      _WaveTexture        );
            }

            ErosionComputeShader.SetInt(    "_Width",   Width   );
            ErosionComputeShader.SetInt(    "_Height",  Height  );
            ErosionComputeShader.GetKernelThreadGroupSizes(_Kernels[0], out _ThreadsPerGroupX, out _ThreadsPerGroupY, out _ThreadsPerGroupZ);

            var kernelInitWaterHeight = ErosionComputeShader.FindKernel("InitWaterHeight");
            ErosionComputeShader.SetTexture(kernelInitWaterHeight, "HeightMap",     _StateTexture      );
            ErosionComputeShader.SetFloat(  "_WaterHeight", Settings.WaterHeight    );
            ErosionComputeShader.Dispatch(
                kernelInitWaterHeight, 
                _StateTexture.width     / (int) _ThreadsPerGroupX,
                _StateTexture.height    / (int) _ThreadsPerGroupY,
                1
            );
        }

        // Debug information
        Debugger.Instance.Display(  "HeightMap",    _StateTexture       );
        Debugger.Instance.Display(  "FluxMap",      _WaterFluxTexture   );
        Debugger.Instance.Display(  "VelocityMap",  _VelocityTexture    );
        Debugger.Instance.Display(  "WaveTexture",  _WaveTexture        );


        /* ========= Setup Rendering ======= */
        // Assign state texture to materials
        foreach (var material in Materials)
        {
            material.SetTexture(StateTextureKey, _StateTexture);
            material.SetTexture(WaveTextureKey, _WaveTexture);
        }
    }  

    public void CaptureDepthMap()
    {
        Debug.Log("capture Depth map");
        // Generate the camera
        if (_DepthCam == null)
        {
            var go = new GameObject("DepthCamera") 
            {
                // hideFlags = HideFlags.HideAndDontSave
            };
            _DepthCam = go.AddComponent<Camera>();
        }

        var AdditionCamData                     = _DepthCam.GetUniversalAdditionalCameraData();
        AdditionCamData.renderShadows           = false;
        AdditionCamData.requiresColorOption     = CameraOverrideOption.Off;
        AdditionCamData.requiresDepthOption     = CameraOverrideOption.Off;

        var DepthCamTransform           = _DepthCam.transform;
        // var DepthExtra = 4.0f;
        DepthCamTransform.position      = new Vector3(256f, 55f, 256f);
        DepthCamTransform.up            = -Vector3.forward;

        _DepthCam.enabled               = true;
        _DepthCam.orthographic          = true;
        _DepthCam.orthographicSize      = 250;
        _DepthCam.nearClipPlane         = -60f;
        _DepthCam.farClipPlane          = -40f;
        _DepthCam.allowHDR              = false;
        _DepthCam.allowMSAA             = false;
        _DepthCam.cullingMask           = BuoyancyMask;

        _DepthCam.targetTexture         = _DepthTexture;
        _DepthCam.Render();


        // _DepthCam.enabled = false;
        // _DepthCam.targetTexture = null;        
    }

    void OnDisable()
    {
        if (_DepthTexture != null)
        {
            DestroyImmediate(_DepthTexture);
        }
    }

    public enum StateChannel : int
    {
        SurfaceHeight = 0,
        Water = 1,
        SuspendedSediment = 2,
        SurfaceHardness = 3
    }  

    public void UpdateStateFromTexture(
        Texture source, 
        int sourceChannel = 0, 
        StateChannel targetStateChannel = StateChannel.SurfaceHeight, 
        float scale = 1f, 
        float bias = 0f
    ) 
    {
        Graphics.Blit(_StateTexture, _StateTextureBuffer);
        Debugger.Instance.Display("SateSource", source);
        _copyMat.SetTexture("_PrevTex", _StateTextureBuffer);
        _copyMat.SetTexture("_HTex", source);
        _copyMat.SetInt("_SrcChannel", sourceChannel);
        _copyMat.SetInt("_TgtChannel", (int)targetStateChannel);
        _copyMat.SetFloat("_Scale", scale);
        _copyMat.SetFloat("_Bias", bias);
        Graphics.Blit(null, _StateTexture, _copyMat);
    }

    public void UpdateSurfaceFromTerrainData(TerrainData terrainData)
    {
        UpdateStateFromTexture(
            terrainData.heightmapTexture,
            0,
            StateChannel.SurfaceHeight,
            scale: terrainData.size.y * 2
        );
    }

    public void OnGUI()
    {
        var inputModes = new[] { "Add water", "Remove water", "Add Terrain", "Remove terrain" };
        GUILayout.BeginArea(new Rect(10, 10, 400, 400));        

        GUILayout.BeginHorizontal();
        GUILayout.Label("Brush strength");
        BrushAmount = GUILayout.HorizontalSlider(BrushAmount, 1f, 100f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Brush size");
        _brushRadius = GUILayout.HorizontalSlider(_brushRadius, 0.01f, 0.2f);
        GUILayout.EndHorizontal();

        GUILayout.Label("[W][A][S][D] - Fly, hold [Shift] - fly faster");
        GUILayout.Label("hold [RMB] - rotate camera");
        GUILayout.Label("[LMB] - draw");
        GUILayout.EndArea();
    }
}
