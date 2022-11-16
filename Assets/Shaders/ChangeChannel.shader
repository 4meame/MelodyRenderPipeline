Shader "Unlit/ChangeChannel"                                 
{                                                              
   Properties                                                  
   {                                                           
       _Channel_R("Texture", 2D) = "black" { }             
       _Channel_G("Texture", 2D) = "black" { }             
       _Channel_B("Texture", 2D) = "black" { }             
       _Channel_A("Texture", 2D) = "black" { }             
   }                                                           
   SubShader                                                   
   {                                                           
       Tags { "RenderType"="Opaque" }                      
       LOD 100                                                 
       Pass                                                    
       {                                                       
           CGPROGRAM                                           
           #pragma vertex vert                                 
           #pragma fragment frag                               
           # include "UnityCG.cginc"                         
           struct appdata {                                    
           float4 vertex : POSITION;                           
           float2 uv : TEXCOORD0;                              
           };                                                  
           struct v2f                                          
           {                                                   
               float2 uv : TEXCOORD0;                          
               UNITY_FOG_COORDS(1)                             
               float4 vertex : SV_POSITION;                    
           };                                                  
           sampler2D _Channel_R;                               
           sampler2D _Channel_G;                               
           sampler2D _Channel_B;                               
           sampler2D _Channel_A;                               
           float4 _Channel_R_ST;                               
           float4 _Channel_G_ST;                               
           float4 _Channel_B_ST;                               
           float4 _Channel_A_ST;                               
           v2f vert(appdata v)                                 
           {                                                   
               v2f o;                                          
               o.vertex = UnityObjectToClipPos(v.vertex);      
               o.uv = TRANSFORM_TEX(v.uv, _Channel_R);         
               return o;                                       
           }                                                   
           float4 frag(v2f i) : SV_Target                      
           {                                                   
               float r = tex2D(_Channel_R, i.uv).x;            
               float g = tex2D(_Channel_G, i.uv).x;            
               float b = tex2D(_Channel_B, i.uv).x;            
               float a = tex2D(_Channel_A, i.uv).x;            
               return float4(r,g,b,a);    
           }                                                   
           ENDCG                                               
       }                                                       
   }                                                           
}                                                              
