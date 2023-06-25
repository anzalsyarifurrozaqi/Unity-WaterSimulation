using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sandbox : MonoBehaviour
{
    public GameObject obj;
    public ComputeShader cs;
    public RenderTexture tex;
    public void Start()
    {
        tex = new RenderTexture(1024, 1024, 24);
        tex.enableRandomWrite = true;
        tex.Create();
        cs.SetTexture(0, "Result", tex);   
        cs.SetFloat("Resolution", tex.width);
        cs.SetFloat("_Step", 5.0f / tex.width);
        cs.Dispatch(0, tex.width / 8, tex.width / 8, 1);

        obj.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex);
    }
}
