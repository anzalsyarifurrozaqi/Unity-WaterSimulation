using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class WaterData : MonoBehaviour
{
    // Singleton
    private static WaterData _instance;
    public static WaterData Instance {
        get {
            if (_instance == null)
            {
                _instance = (WaterData) FindObjectOfType(typeof(WaterData));
            }
            return _instance;
        }
    }

    public Wave[] Waves;

    public WaveData WaveData;

    private ComputeShader WaveComputeShader;
    private int KernelHandle;

    private Camera DepthCamera;

    private RenderTexture DepthTexture;

    private int TEXTURE_SIZE;

    private ComputeBuffer WaveBuffer;

    public void Initialize(ComputeShader computeShader, ComputeBuffer waveBuffer, int kernelHandle, int textureSize)
    {
        WaveComputeShader   = computeShader;
        KernelHandle        = kernelHandle;        
        TEXTURE_SIZE        = textureSize;
        WaveBuffer          = waveBuffer;
        
        SetWaves();

        CaptureDepthMap();
    }

    private void SetWaves()
    {
        SetupWaves();

        float MaxWaveHeight = 0;
        foreach(Wave w in Waves)
        {
            MaxWaveHeight += w.Amplitude;
        }

        MaxWaveHeight /= Waves.Length;
        
        float WaveHeight = transform.position.y;

        WaveComputeShader.SetFloat(     "_wave_height",         WaveHeight                      );
        WaveComputeShader.SetFloat(     "_max_wave_height",     MaxWaveHeight                   );
        WaveComputeShader.SetFloat(     "_max_depth",           WaveData.WaterMaxVisibility     );
        WaveComputeShader.SetInt(       "_wave_count",          Waves.Length                    );

        WaveBuffer.SetData(Waves);        
    }

    private void SetupWaves()
    {
        Random.State BackupSeed                     = Random.state;

        Random.InitState(WaveData.RandomSeed);

        BasicWaves BasicWaves                       = WaveData.BasicWaveSettings;
        float amp                                   = BasicWaves.Amplitude;
        float dir                                   = BasicWaves.Direction;
        float len                                   = BasicWaves.WaveLength;
        int NumWaves                                = BasicWaves.NumWaves;
        Waves                                       = new Wave[NumWaves];

        float r                                     = 1.0f / NumWaves;
        for (int i = 0; i < NumWaves; ++i)
        {
            float p             = Mathf.Lerp(0.5f, 1.5f, i * r);
            float Amplitude     = amp * p * Random.Range(0.8f, 1.2f);
            float Direction     = dir + Random.Range(-90f, 90f);
            float Length        = len * p * Random.Range(0.6f, 1.4f);
            Waves[i]            = new Wave(amp, dir, len);

            Random.InitState(WaveData.RandomSeed + i + 1);
        }
        Random.state = BackupSeed;
    }

    public void CaptureDepthMap()
    {
        if (DepthCamera == null )
        {
            var go          = new GameObject("DepthCamera") {hideFlags = HideFlags.HideAndDontSave};
            DepthCamera     = go.AddComponent<Camera>();
        }

        UniversalAdditionalCameraData AdditionCamData       = DepthCamera.GetUniversalAdditionalCameraData();
        AdditionCamData.renderShadows                       = false;
        AdditionCamData.requiresColorOption                 = CameraOverrideOption.Off;
        AdditionCamData.requiresDepthOption                 = CameraOverrideOption.Off;

        Transform DepthCamTransform                         = DepthCamera.transform;
        float DepthExtra                                    = 4.0f;
        DepthCamTransform.position                          = Vector3.up * (transform.position.y + DepthExtra);
        DepthCamTransform.up                                = Vector3.forward;

        DepthCamera.enabled                                 = true;
        DepthCamera.orthographic                            = true;
        DepthCamera.orthographicSize                        = 250;
        DepthCamera.nearClipPlane                           = 0.01f;
        DepthCamera.farClipPlane                            = WaveData.WaterMaxVisibility + DepthExtra;
        DepthCamera.allowHDR                                = false;
        DepthCamera.allowMSAA                               = false;
        DepthCamera.cullingMask                             = (1 << 10);

        if (!DepthTexture)
        {
            DepthTexture = new RenderTexture(TEXTURE_SIZE, TEXTURE_SIZE, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        }

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
        {
            DepthTexture.filterMode = FilterMode.Point;
        }

        DepthTexture.wrapMode                               = TextureWrapMode.Clamp;
        DepthTexture.name                                   = "Water Depth Map";

        DepthCamera.targetTexture                           = DepthTexture;
        DepthCamera.Render();

        WaveComputeShader.SetTexture(KernelHandle, "_water_depth_map", DepthTexture);

        Vector4 Params = new Vector4(DepthCamTransform.position.y, DepthExtra);
        WaveComputeShader.SetVector("_depth_cam_z_paramps", Params);

        DepthCamera.enabled         = false;
        DepthCamera.targetTexture   = null;
    }
}
