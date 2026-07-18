Shader "JumpTiming/Stage Design Background Key"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyThreshold ("Background Threshold", Range(0, 1)) = 0.09
        _KeyFeather ("Background Feather", Range(0.001, 0.5)) = 0.08
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment StageDesignFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON

            #include "UnitySprites.cginc"

            float _KeyThreshold;
            float _KeyFeather;

            fixed4 StageDesignFrag(v2f IN) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;
                float brightness = max(color.r, max(color.g, color.b));
                float keyedAlpha = smoothstep(
                    _KeyThreshold,
                    _KeyThreshold + max(_KeyFeather, 0.001),
                    brightness);
                color.a *= keyedAlpha;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
