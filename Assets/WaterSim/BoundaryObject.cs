using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryObject : MonoBehaviour
{       
    public ComputeShader ErosionComputeShader;
    private Vector3 _Position; 

    private const string _KernelName = "PressureControl";
    private int _Kernel;
    private uint _ThreadsPerGroupX;
    private uint _ThreadsPerGroupY;
    private uint _ThreadsPerGroupZ;

    private Vector4 _inputControls;

    void Start()
    {
        _Kernel = ErosionComputeShader.FindKernel(_KernelName);

        ErosionComputeShader.GetKernelThreadGroupSizes(_Kernel, out _ThreadsPerGroupX, out _ThreadsPerGroupY, out _ThreadsPerGroupZ);
    }
    void Update()
    {
        var X = transform.position.x / 512;
        var Y = transform.position.z / 512;
        var Radius = 0.025f;
        var Amount = 0f;

        _Position = transform.position;

        if ((int) _Position.y < 20)
        {
            Amount = 0.15f;            
        }
        else
        {
            Amount = 0f;
        }

        _inputControls = new Vector4(X, Y, Radius, Amount);
        Shader.SetGlobalVector("_InputControls", _inputControls);
    }

    void FixedUpdate()
    {
        if (ErosionComputeShader != null)
        {
            // Inputs
            ErosionComputeShader.SetVector("_InputControls", _inputControls);

            ErosionComputeShader.Dispatch(
                _Kernel, 
                512     / (int) _ThreadsPerGroupX,
                512    / (int) _ThreadsPerGroupY,
                1
            );
        }
    }
}
