Shader "Custom/WaterShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _MaxDisplacement         ("water max Displacemnet", Float) = 5
        _DisplacementDistribution("Displacemnet Distribution", Float) = 0.2
        _DisplacementStrength    ("water  Displacemnet, displacement", Float) = 5
        _NoiseTexture("Noise Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }        

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _fountain_pressure_buffer;
            sampler2D _fountain_velocity_buffer;
            sampler2D _NoiseTexture;;

            float4 _fountain_pressure_buffer_ST;
            float4 _fountain_velocity_buffer_ST;    

            float4    _fountain_downLeft;
            float4    _fountain_upRight;
            float     _MaxDisplacement;    
            float     _DisplacementDistribution;
            float     _DisplacementStrength;


            float pressureToneMapping(float pressure)
            {

                float clampValue = clamp(pressure / _MaxDisplacement, -1.0, 1.0);
                      clampValue = pow(abs(clampValue), _DisplacementDistribution) * sign(clampValue);
                      clampValue = lerp(clampValue, pressure/30. , pow(1.-saturate(abs(pressure)),4.2)) * _DisplacementStrength;

                //float clampValue = abs(pressure);
                //      clampValue = clampValue/(clampValue+1.0);
                //      clampValue = pow(abs(clampValue), _DisplacementDistribution) * _DisplacementStrength * sign(pressure);

                return clampValue;
            }

            // -------------------------------------------------------------
            // declaration
            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct Interpolators
            {
                float2 uv        : TEXCOORD0;                
                float4 vertex    : SV_POSITION;
                float4 worldPos  : TEXCOORD1;                
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;                                
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                float4 originalWorldPos = o.worldPos;

                float2 uv  = o.worldPos.zx;                       
               float   pressureBuffer = tex2Dlod(_fountain_pressure_buffer, float4(uv.xy, 0, 0));


               float4 disVector = float4(0.,0., 0., 0.);

                o.worldPos  = mul(unity_ObjectToWorld, v.vertex + disVector);
                o.vertex    = UnityObjectToClipPos(v.vertex + disVector);                                
                return o;
            }

            float4 frag (Interpolators i) : SV_Target
            {
                float4 col = tex2D(_fountain_pressure_buffer, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
