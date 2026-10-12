// Recolours a clothing / hair texture (used once per change, by PlayerLook, to make a dyed copy).
// Keeps the shading and fabric detail from the original's brightness and replaces its colour, so dark
// clothes can be dyed as well as light ones (a plain colour multiply would leave them dark).
Shader "Hidden/Hitman/Dye"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Dye ("Dye", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Dye;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 g = IsGammaSpace() ? c.rgb : LinearToGammaSpace(c.rgb);
                float3 dye = IsGammaSpace() ? _Dye.rgb : LinearToGammaSpace(_Dye.rgb);
                // sqrt spreads out the dark end so creases and seams in dark fabric still show.
                float shade = 0.3 + 1.05 * sqrt(dot(g, float3(0.299, 0.587, 0.114)));
                float3 o = saturate(dye * shade);
                return float4(IsGammaSpace() ? o : GammaToLinearSpace(o), c.a);
            }
            ENDCG
        }
    }
}
