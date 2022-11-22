// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

Shader "Crest/Ocean"
{
	Properties
	{
		[Header(Normals)]
		// Strength of the final surface normal (includes both wave normal and normal map)
		_NormalsStrengthOverall( "Overall Normal Strength", Range( 0.0, 1.0 ) ) = 1.0
		// Whether to add normal detail from a texture. Can be used to add visual detail to the water surface
		[Toggle] _ApplyNormalMapping("Use Normal Map", Float) = 1
		// Normal map texture (should be set to Normals type in the properties)
		[NoScaleOffset] _Normals("Normal Map", 2D) = "bump" {}
		// Scale of normal map texture
		_NormalsScale("Normal Map Scale", Range(0.01, 200.0)) = 40.0
		// Strength of normal map influence
		_NormalsStrength("Normal Map Strength", Range(0.01, 2.0)) = 0.36

		// Base light scattering settings which give water colour
		[Header(Scattering)]
		// Base colour when looking straight down into water
		_Diffuse("Scatter Colour Base", Color) = (0.0, 0.0026954073, 0.16981131, 1.0)
		// Base colour when looking into water at shallow/grazing angle
		_DiffuseGrazing("Scatter Colour Grazing", Color) = (0.0, 0.003921569, 0.1686274, 1.0)
		// Changes colour in shadow. Requires 'Create Shadow Data' enabled on OceanRenderer script.
		[Toggle] _Shadows("Shadowing", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
		// Base colour in shadow
		_DiffuseShadow("Scatter Colour Shadow", Color) = (0.0, 0.0013477041, 0.084905684, 1.0)

		[Header(Shallow Scattering)]
		// Enable light scattering in shallow water
		[Toggle] _SubSurfaceShallowColour("Enable", Float) = 0
		// Colour in shallow water
		_SubSurfaceShallowCol("Scatter Colour Shallow", Color) = (0.0, 0.003921569, 0.24705884, 1.0)
		// Max depth that is considered 'shallow'
		_SubSurfaceDepthMax("Scatter Colour Shallow Depth Max", Range(0.01, 50.0)) = 10.0
		// Fall off of shallow scattering
		_SubSurfaceDepthPower("Scatter Colour Shallow Depth Falloff", Range(0.01, 10.0)) = 2.5
		// Shallow water colour in shadow (see comment on Shadowing param above)
		_SubSurfaceShallowColShadow("Scatter Colour Shallow Shadow", Color) = (0.0, 0.0053968453, 0.17, 1)

		[Header(Subsurface Scattering)]
		// Whether to to emulate light scattering through the water volume
		[Toggle] _SubSurfaceScattering("Enable", Float) = 1
		// Colour tint for primary light contribution
		_SubSurfaceColour("SSS Tint", Color) = (0.08850684, 0.497, 0.45615074, 1.0)
		// Amount of primary light contribution that always comes in
		_SubSurfaceBase("SSS Intensity Base", Range(0.0, 4.0)) = 0.0
		// Primary light contribution in direction of light to emulate light passing through waves
		_SubSurfaceSun("SSS Intensity Sun", Range(0.0, 10.0)) = 1.7
		// Fall-off for primary light scattering to affect directionality
		_SubSurfaceSunFallOff("SSS Sun Falloff", Range(1.0, 16.0)) = 5.0

		// Reflection properites
		[Header(Reflection Environment)]
		// Strength of specular lighting response
		_Specular("Specular", Range(0.0, 1.0)) = 0.7
		// Smoothness of surface
		_Smoothness("Smoothness", Range(0.0, 1.0)) = 0.8
		// Vary smoothness - helps to spread out specular highlight in mid-to-background. Models transfer of normal detail
		// to microfacets in BRDF.
		[Toggle] _VarySmoothnessOverDistance("Vary Smoothness Over Distance", Float) = 0
		// Material smoothness at far distance from camera
		_SmoothnessFar("Smoothness Far", Range(0.0, 1.0)) = 0.35
		// Definition of far distance
		_SmoothnessFarDistance("Smoothness Far Distance", Range(1.0, 8000.0)) = 2000.0
		// How smoothness varies between near and far distance - shape of curve.
		_SmoothnessPower("Smoothness Falloff", Range(0.0, 2.0)) = 0.5
		// Acts as mip bias to smooth/blur reflection
		_ReflectionBlur("Softness", Range(0.0, 7.0)) = 0.0
		// Main light intensity multiplier
		_LightIntensityMultiplier("Light Intensity Multiplier", Range(0.0, 10.0)) = 1.0
		// Controls harshness of Fresnel behaviour
		_FresnelPower("Fresnel Power", Range(1.0, 20.0)) = 5.0
		// Index of refraction of air. Can be increased to almost 1.333 to increase visibility up through water surface.
		_RefractiveIndexOfAir("Refractive Index of Air", Range(1.0, 2.0)) = 1.0
		// Index of refraction of water. Typically left at 1.333.
		_RefractiveIndexOfWater("Refractive Index of Water", Range(1.0, 2.0)) = 1.333
		// Dynamically rendered 'reflection plane' style reflections. Requires OceanPlanarReflection script added to main camera.
		[Toggle] _PlanarReflections("Planar Reflections", Float) = 0
		// How much the water normal affects the planar reflection
		_PlanarReflectionNormalsStrength("Planar Reflections Distortion", Float) = 1
		// Multiplier to adjust how intense the reflection is
		_PlanarReflectionIntensity("Planar Reflection Intensity", Range(0.0, 1.0)) = 1.0

		[Header(Procedural Skybox)]
		// Enable a simple procedural skybox, not suitable for realistic reflections, but can be useful to give control over reflection colour
		// especially in stylized/non realistic applications
		[Toggle] _ProceduralSky("Enable", Float) = 0
		// Base sky colour
		[HDR] _SkyBase("Base", Color) = (1.0, 1.0, 1.0, 1.0)
		// Colour in sun direction
		[HDR] _SkyTowardsSun("Towards Sun", Color) = (1.0, 1.0, 1.0, 1.0)
		// Direction fall off
		_SkyDirectionality("Directionality", Range(0.0, 0.99)) = 1.0
		// Colour away from sun direction
		[HDR] _SkyAwayFromSun("Away From Sun", Color) = (1.0, 1.0, 1.0, 1.0)

		[Header(Foam)]
		// Enable foam layer on ocean surface
		[Toggle] _Foam("Enable", Float) = 1
		// Foam texture
		[NoScaleOffset] _FoamTexture("Foam", 2D) = "white" {}
		// Foam texture scale
		_FoamScale("Foam Scale", Range(0.01, 50.0)) = 10.0
		// Controls how gradual the transition is from full foam to no foam
		_WaveFoamFeather("Foam Feather", Range(0.001, 1.0)) = 0.4
		// Scale intensity of lighting
		_WaveFoamLightScale("Foam Light Scale", Range(0.0, 2.0)) = 1.35
		// Colour tint for whitecaps / foam on water surface
		_FoamWhiteColor("Foam Tint", Color) = (1.0, 1.0, 1.0, 1.0)
		// Proximity to sea floor where foam starts to get generated
		_ShorelineFoamMinDepth("Shoreline Foam Min Depth", Range(0.01, 5.0)) = 0.27

		[Header(Foam 3D Lighting)]
		// Generates normals for the foam based on foam values/texture and use it for foam lighting
		[Toggle] _Foam3DLighting("Enable", Float) = 1
		// Strength of the generated normals
		_WaveFoamNormalStrength("Foam Normal Strength", Range(0.0, 30.0)) = 3.5
		// Acts like a gloss parameter for specular response
		_WaveFoamSpecularFallOff("Specular Falloff", Range(1.0, 512.0)) = 293.0
		// Strength of specular response
		_WaveFoamSpecularBoost("Specular Boost", Range(0.0, 16.0)) = 0.15

		[Header(Foam Bubbles)]
		// Colour tint bubble foam underneath water surface
		_FoamBubbleColor("Foam Bubbles Color", Color) = (0.64, 0.83, 0.82, 1.0)
		// Parallax for underwater bubbles to give feeling of volume
		_FoamBubbleParallax("Foam Bubbles Parallax", Range(0.0, 0.5)) = 0.14
		// How much underwater bubble foam is generated
		_WaveFoamBubblesCoverage("Foam Bubbles Coverage", Range(0.0, 5.0)) = 1.68

		[Header(Transparency)]
		// Whether light can pass through the water surface
		[Toggle] _Transparency("Enable", Float) = 1
		// Scattering coefficient within water volume, per channel
		_DepthFogDensity("Depth Fog Density", Vector) = (0.9, 0.3, 0.35, 1.0)
		// How strongly light is refracted when passing through water surface
		_RefractionStrength("Refraction Strength", Range(0.0, 2.0)) = 0.5

		[Header(Caustics)]
		// Approximate rays being focused/defocused on underwater surfaces
		[Toggle] _Caustics("Enable", Float) = 1
		// Caustics texture
		[NoScaleOffset] _CausticsTexture("Caustics", 2D) = "black" {}
		// Caustics texture scale
		_CausticsTextureScale("Caustics Scale", Range(0.0, 25.0)) = 5.0
		// The 'mid' value of the caustics texture, around which the caustic texture values are scaled
		_CausticsTextureAverage("Caustics Texture Grey Point", Range(0.0, 1.0)) = 0.07
		// Scaling / intensity
		_CausticsStrength("Caustics Strength", Range(0.0, 10.0)) = 3.2
		// The depth at which the caustics are in focus
		_CausticsFocalDepth("Caustics Focal Depth", Range(0.0, 250.0)) = 2.0
		// The range of depths over which the caustics are in focus
		_CausticsDepthOfField("Caustics Depth of Field", Range(0.01, 1000.0)) = 0.33
		// How much the caustics texture is distorted
		_CausticsDistortionStrength("Caustics Distortion Strength", Range(0.0, 0.25)) = 0.16
		// The scale of the distortion pattern used to distort the caustics
		_CausticsDistortionScale("Caustics Distortion Scale", Range(0.01, 50.0)) = 25.0

		// To use the underwater effect the UnderWaterCurtainGeom and UnderWaterMeniscus prefabs must be parented to the camera.
		[Header(Underwater)]
		// Whether the underwater effect is being used. This enables code that shades the surface correctly from underneath.
		[Toggle] _Underwater("Enable", Float) = 0
		// Ordinarily set this to Back to cull back faces, but set to Off to make sure both sides of the surface draw if the
		// underwater effect is being used.
		[Enum(CullMode)] _CullMode("Cull Mode", Int) = 2

		[Header(Flow)]
		// Flow is horizontal motion in water as demonstrated in the 'whirlpool' example scene. 'Create Flow Sim' must be
		// enabled on the OceanRenderer to generate flow data.
		[Toggle] _Flow("Enable", Float) = 0

		[Header(Clip Surface)]
		// Discards ocean surface pixels. Requires 'Create Clip Surface Data' enabled on OceanRenderer script.
		[Toggle] _ClipSurface("Enable", Float) = 0
		// Clips purely based on water depth
		[Toggle] _ClipUnderTerrain("Clip Below Terrain (Requires depth cache)", Float) = 0

		[Header(Albedo)]
		// Albedo is a colour that is composited onto the surface. Requires 'Create Albedo Data' enabled on OceanRenderer component.
		[Toggle] _Albedo("Enable", Float) = 0

		[Header(Rendering)]
		// What projection modes will this material support? Choosing perspective or orthographic is an optimisation.
		[KeywordEnum(Both, Perspective, Orthographic)] _Projection("Projection Support", Float) = 0.0
	}

	SubShader
	{
		Tags
		{
			"RenderType"="Transparent"
			"Queue"="Transparent-100"
			"DisableBatching"="True"
		}

		Pass
		{
			// Following URP code. Apparently this can be not defined according to https://gist.github.com/phi-lira/225cd7c5e8545be602dca4eb5ed111ba
			Tags {"LightMode" = "MelodyDeferred"}

			// Need to set this explicitly as we dont rely on built-in pipeline render states anymore.
			ZWrite On

			// Culling user defined - can be inverted for under water
			Cull[_CullMode]

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard SRP library
			// All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
			// https://gist.github.com/phi-lira/225cd7c5e8545be602dca4eb5ed111ba
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			// For VFACE
			#pragma target 3.0

			#pragma vertex Vert
			#pragma fragment Frag

			#pragma multi_compile_fog

			// #pragma enable_d3d11_debug_symbols

			// Only support additional lights in fragment shader as vertex lights require normals in vertex shader.
			#pragma multi_compile_fragment _ _RECEIVE_SHADOWS
			#pragma multi_compile_fragment _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile_fragment _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile_fragment _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile_fragment _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE

			#pragma shader_feature_local _APPLYNORMALMAPPING_ON
			#pragma shader_feature_local _SUBSURFACESCATTERING_ON
			#pragma shader_feature_local _SUBSURFACESHALLOWCOLOUR_ON
			#pragma shader_feature_local _VARYSMOOTHNESSOVERDISTANCE_ON
			#pragma shader_feature_local _TRANSPARENCY_ON
			#pragma shader_feature_local _CAUSTICS_ON
			#pragma shader_feature_local _FOAM_ON
			#pragma shader_feature_local _FOAM3DLIGHTING_ON
			#pragma shader_feature_local _PLANARREFLECTIONS_ON
			//#pragma shader_feature_local _OVERRIDEREFLECTIONCUBEMAP_ON

			#pragma shader_feature_local _PROCEDURALSKY_ON
			#pragma shader_feature_local _UNDERWATER_ON
			#pragma shader_feature_local _FLOW_ON
			#pragma shader_feature_local _SHADOWS_ON
			#pragma shader_feature_local _CLIPSURFACE_ON
			#pragma shader_feature_local _CLIPUNDERTERRAIN_ON
			#pragma shader_feature_local _ALBEDO_ON

			#pragma shader_feature_local _ _PROJECTION_PERSPECTIVE _PROJECTION_ORTHOGRAPHIC

			#pragma multi_compile _ CREST_UNDERWATER_BEFORE_TRANSPARENT

			// Clipping the ocean surface for underwater volumes.
			#pragma multi_compile _ CREST_WATER_VOLUME_2D CREST_WATER_VOLUME_HAS_BACKFACE

			#pragma multi_compile _ CREST_FLOATING_ORIGIN

			#include "../../ShaderLibrary/Common.hlsl"
			#include "../../ShaderLibrary/Surface.hlsl"
			#include "../../ShaderLibrary/Shadows.hlsl"
			#include "../../ShaderLibrary/Light.hlsl"
			#include "../../ShaderLibrary/BRDF.hlsl"
			#include "../../ShaderLibrary/GI.hlsl"
			#include "../../ShaderLibrary/Lighting.hlsl"

			// @Hack: Work around to unity_CameraToWorld._13_23_33 not being set correctly in URP 7.4+
			float3 _CameraForward;

			#include "ShaderLibrary/Common.hlsl"

			#include "OceanGlobals.hlsl"
			#include "OceanInputsDriven.hlsl"
			#include "OceanShaderData.hlsl"
			#include "OceanHelpersNew.hlsl"
			#include "OceanVertHelpers.hlsl"
			#include "OceanShaderHelpers.hlsl"
			#include "OceanLightingHelpers.hlsl"

			#include "Helpers/WaterVolume.hlsl"

			#include "OceanEmission.hlsl"
			#include "OceanNormalMapping.hlsl"
			#include "OceanReflection.hlsl"
			#include "OceanFoam.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;

				UNITY_VERTEX_INPUT_INSTANCE_ID
				//UV Coordinate for the light map
				GI_ATTRIBUTE_DATA
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 lodAlpha_worldXZUndisplaced_oceanDepth : TEXCOORD0;
				real4 n_shadow : TEXCOORD1;
				real3 screenPosXYW : TEXCOORD2;
				float4 positionWS : TEXCOORD3;
				#if _FLOW_ON
				real2 flow : TEXCOORD4;
				#endif
				float2 seaLevelDerivs : TEXCOORD5;
				//UV Coordinate for the light map
				GI_VARYINGS_DATA
			};

			Varyings Vert(Attributes input)
			{
				Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(input);

				//extract the UV Coordinate from the light map data
				TRANSFER_GI_DATA(input, o);

				const CascadeParams cascadeData0 = _CrestCascadeData[_LD_SliceIndex];
				const CascadeParams cascadeData1 = _CrestCascadeData[_LD_SliceIndex + 1];
				const PerCascadeInstanceData instanceData = _CrestPerCascadeInstanceData[_LD_SliceIndex];

				// Move to world space
				o.positionWS.xyz = TransformObjectToWorld(input.positionOS);

				// Vertex snapping and lod transition
				float lodAlpha;
				const float meshScaleLerp = instanceData._meshScaleLerp;
				const float gridSize = instanceData._geoGridWidth;
				SnapAndTransitionVertLayout(meshScaleLerp, cascadeData0, gridSize, o.positionWS.xyz, lodAlpha);

				{
					// Scale up by small "epsilon" to solve numerical issues. Expand slightly about tile center.
					// :OceanGridPrecisionErrors
					const float2 tileCenterXZ = UNITY_MATRIX_M._m03_m23;
					const float2 cameraPositionXZ = abs(_WorldSpaceCameraPos.xz);
					// Scale "epsilon" by distance from zero. There is an issue where overlaps can cause SV_IsFrontFace
					// to be flipped (needs to be investigated). Gaps look bad from above surface, and overlaps look bad
					// from below surface. We want to close gaps without introducing overlaps. A fixed "epsilon" will
					// either not solve gaps at large distances or introduce too many overlaps at small distances. Even
					// with scaling, there are still unsolvable overlaps underwater (especially at large distances).
					// 100,000 (0.00001) is the maximum position before Unity warns the user of precision issues.
					o.positionWS.xz = lerp(tileCenterXZ, o.positionWS.xz, lerp(1.0, 1.01, max(cameraPositionXZ.x, cameraPositionXZ.y) * 0.00001));
				}

				o.lodAlpha_worldXZUndisplaced_oceanDepth.x = lodAlpha;
				o.lodAlpha_worldXZUndisplaced_oceanDepth.yz = o.positionWS.xz;
				o.lodAlpha_worldXZUndisplaced_oceanDepth.w = CREST_OCEAN_DEPTH_BASELINE;
				// Sample shape textures - always lerp between 2 LOD scales, so sample two textures

				// Calculate sample weights. params.z allows shape to be faded out (used on last lod to support pop-less scale transitions)
				const float wt_smallerLod = (1. - lodAlpha) * cascadeData0._weight;
				const float wt_biggerLod = (1. - wt_smallerLod) * cascadeData1._weight;
				// Sample displacement textures, add results to current world pos / normal / foam
				const float2 positionWS_XZ_before = o.positionWS.xz;

				// Data that needs to be sampled at the undisplaced position
				if (wt_smallerLod > 0.001)
				{
					const float3 uv_slice_smallerLod = WorldToUV(positionWS_XZ_before, cascadeData0, _LD_SliceIndex);

					#if !_DEBUGDISABLESHAPETEXTURES_ON
					SampleDisplacements(_LD_TexArray_AnimatedWaves, uv_slice_smallerLod, wt_smallerLod, o.positionWS.xyz);
					#endif

					#if _FLOW_ON
					SampleFlow(_LD_TexArray_Flow, uv_slice_smallerLod, wt_smallerLod, o.flow);
					#endif
				}
				if (wt_biggerLod > 0.001)
				{
					const float3 uv_slice_biggerLod = WorldToUV(positionWS_XZ_before, cascadeData1, _LD_SliceIndex + 1);

					#if !_DEBUGDISABLESHAPETEXTURES_ON
					SampleDisplacements(_LD_TexArray_AnimatedWaves, uv_slice_biggerLod, wt_biggerLod, o.positionWS.xyz);
					#endif

					#if _FLOW_ON
					SampleFlow(_LD_TexArray_Flow, uv_slice_biggerLod, wt_biggerLod, o.flow.xy);
					#endif
				}

				// Data that needs to be sampled at the displaced position
				half seaLevelOffset = 0.0;
				o.seaLevelDerivs = 0.0;
				if (wt_smallerLod > 0.0001)
				{
					const float3 uv_slice_smallerLodDisp = WorldToUV(o.positionWS.xz, cascadeData0, _LD_SliceIndex);

					SampleSeaDepth(_LD_TexArray_SeaFloorDepth, uv_slice_smallerLodDisp, wt_smallerLod, o.lodAlpha_worldXZUndisplaced_oceanDepth.w, seaLevelOffset, cascadeData0, o.seaLevelDerivs);

					#if _SHADOWS_ON
					// The minimum sampling weight is lower than others to fix shallow water colour popping.
					if (wt_smallerLod > 0.001)
					{
						SampleShadow(_LD_TexArray_Shadow, uv_slice_smallerLodDisp, wt_smallerLod, o.n_shadow.zw);
					}
					#endif
				}
				if (wt_biggerLod > 0.0001)
				{
					const float3 uv_slice_biggerLodDisp = WorldToUV(o.positionWS.xz, cascadeData1, _LD_SliceIndex + 1);

					SampleSeaDepth(_LD_TexArray_SeaFloorDepth, uv_slice_biggerLodDisp, wt_biggerLod, o.lodAlpha_worldXZUndisplaced_oceanDepth.w, seaLevelOffset, cascadeData1, o.seaLevelDerivs);

					#if _SHADOWS_ON
					// The minimum sampling weight is lower than others to fix shallow water colour popping.
					if (wt_biggerLod > 0.001)
					{
						SampleShadow(_LD_TexArray_Shadow, uv_slice_biggerLodDisp, wt_biggerLod, o.n_shadow.zw);
					}
					#endif
				}

				o.positionWS.y += seaLevelOffset;

				o.positionCS = TransformWorldToHClip(o.positionWS.xyz);

				o.screenPosXYW = ComputeScreenPos(o.positionCS).xyw;

				return o;
			}

			half4 Frag(const Varyings input, const bool i_isFrontFace : SV_IsFrontFace) : SV_Target
			{
#if _CLIPSURFACE_ON
				{
					// Clip surface
					half clipValue = 0.0;

					uint slice0; uint slice1; float alpha;
					PosToSliceIndices(input.positionWS.xz, 0.0, _CrestCascadeData[0]._scale, slice0, slice1, alpha);

					const CascadeParams cascadeData0 = _CrestCascadeData[slice0];
					const CascadeParams cascadeData1 = _CrestCascadeData[slice1];
					const float weight0 = (1.0 - alpha) * cascadeData0._weight;
					const float weight1 = (1.0 - weight0) * cascadeData1._weight;

					if (weight0 > 0.001)
					{
						const float3 uv = WorldToUV(input.positionWS.xz, cascadeData0, slice0);
						SampleClip(_LD_TexArray_ClipSurface, uv, weight0, clipValue);
					}
					if (weight1 > 0.001)
					{
						const float3 uv = WorldToUV(input.positionWS.xz, cascadeData1, slice1);
						SampleClip(_LD_TexArray_ClipSurface, uv, weight1, clipValue);
					}

					clipValue = lerp(_CrestClipByDefault, clipValue, weight0 + weight1);
					// Add 0.5 bias to tighten and smooth clipped edges.
					clip(-clipValue + 0.5);
				}
#endif // _CLIPSURFACE_ON

				const CascadeParams cascadeData0 = _CrestCascadeData[_LD_SliceIndex];
				const CascadeParams cascadeData1 = _CrestCascadeData[_LD_SliceIndex + 1];
				const PerCascadeInstanceData instanceData = _CrestPerCascadeInstanceData[_LD_SliceIndex];

				#if _UNDERWATER_ON
				const bool underwater = IsUnderwater(i_isFrontFace, _CrestForceUnderwater);
				#else
				const bool underwater = false;
				#endif

				const float lodAlpha = input.lodAlpha_worldXZUndisplaced_oceanDepth.x;
				const float2 positionXZWSUndisplaced = input.lodAlpha_worldXZUndisplaced_oceanDepth.yz;
				const float wt_smallerLod = (1.0 - lodAlpha) * cascadeData0._weight;
				const float wt_biggerLod = (1.0 - wt_smallerLod) * cascadeData1._weight;

				real3 screenPos = input.screenPosXYW;
				real2 uvDepth = screenPos.xy / screenPos.z;

#if CREST_WATER_VOLUME
				ApplyVolumeToOceanSurface(input.positionCS);
#endif

				#if _CLIPUNDERTERRAIN_ON
				clip(input.lodAlpha_worldXZUndisplaced_oceanDepth.w + 2.0);
				#endif

				real3 view = normalize(_WorldSpaceCameraPos - input.positionWS.xyz);

				float pixelZ = CrestLinearEyeDepth(input.positionCS.z);
				// Raw depth is logarithmic for perspective, and linear (0-1) for orthographic.
				float rawDepth = CREST_SAMPLE_SCENE_DEPTH_X(uvDepth);
				float sceneZ = CrestLinearEyeDepth(rawDepth);

				// Normal - geom + normal mapping. Subsurface scattering.
				float3 dummy = 0.;
				float3 n_pixel = float3(0.0, 1.0, 0.0);
				half sss = 0.;
				#if _FOAM_ON
				float foam = 0.0;
				#endif
				#if _ALBEDO_ON
				half4 albedo = 0.0;
				#endif
				if (wt_smallerLod > 0.001)
				{
					const float3 uv_slice_smallerLod = WorldToUV(positionXZWSUndisplaced, cascadeData0, _LD_SliceIndex);
					SampleDisplacementsNormals(_LD_TexArray_AnimatedWaves, uv_slice_smallerLod, wt_smallerLod, cascadeData0._oneOverTextureRes, cascadeData0._texelWidth, dummy, n_pixel.xz, sss);

					#if _FOAM_ON
					SampleFoam(_LD_TexArray_Foam, uv_slice_smallerLod, wt_smallerLod, foam);
					#endif

					#if _ALBEDO_ON
					SampleAlbedo(_LD_TexArray_Albedo, uv_slice_smallerLod, wt_smallerLod, albedo);
					#endif
				}
				if (wt_biggerLod > 0.001)
				{
					const float3 uv_slice_biggerLod = WorldToUV(positionXZWSUndisplaced, cascadeData1, _LD_SliceIndex + 1);
					SampleDisplacementsNormals(_LD_TexArray_AnimatedWaves, uv_slice_biggerLod, wt_biggerLod, cascadeData1._oneOverTextureRes, cascadeData1._texelWidth, dummy, n_pixel.xz, sss);

					#if _FOAM_ON
					SampleFoam(_LD_TexArray_Foam, uv_slice_biggerLod, wt_biggerLod, foam);
					#endif

					#if _ALBEDO_ON
					SampleAlbedo(_LD_TexArray_Albedo, uv_slice_biggerLod, wt_biggerLod, albedo);
					#endif
				}

#if _SUBSURFACESCATTERING_ON
				// Extents need the default SSS to avoid popping and not being noticeably different.
				if (_LD_SliceIndex == ((uint)_SliceCount - 1))
				{
					sss = CREST_SSS_MAXIMUM - CREST_SSS_RANGE;
				}
#endif

				#if _APPLYNORMALMAPPING_ON
				#if _FLOW_ON
				ApplyNormalMapsWithFlow(_NormalsTiledTexture, positionXZWSUndisplaced, input.flow.xy, lodAlpha, cascadeData0, instanceData, n_pixel);
				#else
				n_pixel.xz += SampleNormalMaps(_NormalsTiledTexture, positionXZWSUndisplaced, 0.0, lodAlpha, cascadeData0, instanceData);
				#endif
				#endif

				n_pixel.xz += float2(-input.seaLevelDerivs.x, -input.seaLevelDerivs.y);

				// Finalise normal
				n_pixel.xz *= _NormalsStrengthOverall;
				n_pixel = normalize( n_pixel );
				if (underwater) n_pixel = -n_pixel;

				Surface surfaceData;
				//init surface data to rely on pipeline bilut-in method for now
				surfaceData.position = input.positionWS.xyz;
				surfaceData.normal = n_pixel;
				surfaceData.interpolatedNormal = n_pixel;
				surfaceData.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS.xyz);
				surfaceData.depth = -TransformWorldToView(input.positionWS.xyz).z;
				surfaceData.color = 1.0;
				surfaceData.alpha = 1.0;
				surfaceData.metallic = 1.0;
				surfaceData.occlusion = 1.0;
				surfaceData.smoothness = _Smoothness;
				surfaceData.dither = 0;
				surfaceData.fresnelStrength = 0.0;
				ShadowData shadowData;
				shadowData = GetShadowData(surfaceData);
				const Light lightMain = GetMainLight(surfaceData, shadowData);
				const real3 lightDir = lightMain.direction;
				real3 lightCol = lightMain.color * lightMain.distanceAttenuation;
				// No additional light direction as is only used for sun light SSS and caustics.
				real3 additionalLightCol = 0.0;
				for (int j = 0; j < GetOtherLightCount(); j++) {
					Light light = GetOtherLight(j, surfaceData, shadowData);
					additionalLightCol += light.color * (light.distanceAttenuation * light.shadowAttenuation);
				}

				const real2 shadow = 1.0
				#if _SHADOWS_ON
					- input.n_shadow.zw
				#endif
					;

				// Foam - underwater bubbles and whitefoam
				real3 bubbleCol = (half3)0.;
				#if _FOAM_ON
				// Foam can saturate.
				foam = saturate(foam);

				real4 whiteFoamCol;
				#if !_FLOW_ON
				ComputeFoam
				(
					_FoamTiledTexture,
					foam,
					positionXZWSUndisplaced,
					input.positionWS.xz,
					0.0, // Flow
					n_pixel,
					pixelZ,
					sceneZ,
					view,
					lightDir,
					lightCol,
					additionalLightCol,
					shadow.y,
					lodAlpha,
					bubbleCol,
					whiteFoamCol,
					cascadeData0,
					cascadeData1
				);
				#else
				ComputeFoamWithFlow
				(
					_FoamTiledTexture,
					input.flow,
					foam,
					positionXZWSUndisplaced,
					input.positionWS.xz,
					n_pixel,
					pixelZ,
					sceneZ,
					view,
					lightDir,
					lightCol,
					additionalLightCol,
					shadow.y,
					lodAlpha,
					bubbleCol,
					whiteFoamCol,
					cascadeData0,
					cascadeData1
				);
				#endif // _FLOW_ON
				#endif // _FOAM_ON

				// Compute color of ocean - in-scattered light + refracted scene
				half3 scatterCol = ScatterColour
				(
					input.lodAlpha_worldXZUndisplaced_oceanDepth.w,
#if defined(CREST_UNDERWATER_BEFORE_TRANSPARENT) && defined(_SHADOWS_ON)
					underwater ? UnderwaterShadowSSS(_WorldSpaceCameraPos.xz) :
#endif
					shadow.x,
					sss,
					view,
#if CREST_UNDERWATER_BEFORE_TRANSPARENT
					underwater ? _CrestAmbientLighting :
#endif
					WaveHarmonic::Crest::AmbientLight(),
					lightDir,
					lightCol,
					additionalLightCol,
					underwater
				);
				half3 col = OceanEmission
				(
					view,
					n_pixel,
					lightDir,
					screenPos,
					pixelZ,
					input.positionCS.z,
					uvDepth,
					input.positionCS.xy,
					sceneZ,
					rawDepth,
					bubbleCol,
					underwater,
					scatterCol,
					cascadeData0,
					cascadeData1,
					surfaceData
				);

				// Light that reflects off water surface

				// Soften reflection at intersections with objects/surfaces
				#if _TRANSPARENCY_ON
				// Above water depth outline is handled in OceanEmission.
				sceneZ = (underwater ? CrestLinearEyeDepth(CREST_MULTISAMPLE_SCENE_DEPTH(uvDepth, rawDepth)) : sceneZ);
				float reflAlpha = saturate((sceneZ  - pixelZ) / 0.2);
				#else
				// This addresses the problem where screenspace depth doesnt work in VR, and so neither will this. In VR people currently
				// disable transparency, so this will always be 1.0.
				float reflAlpha = 1.0;
				#endif

				#if _UNDERWATER_ON
				if (underwater)
				{
					ApplyReflectionUnderwater(view, n_pixel, lightDir, shadow.y, screenPos.xyzz, scatterCol, reflAlpha, col);
				}
				else
				#endif
				{
					ApplyReflectionSky(view, n_pixel, lightDir, shadow.y, screenPos.xyzz, pixelZ, reflAlpha, surfaceData, col);
				}

				// Override final result with white foam - bubbles on surface
				#if _FOAM_ON
				col = lerp(col, whiteFoamCol.rgb, whiteFoamCol.a);
				#endif

				// Composite albedo input on top
				#if _ALBEDO_ON
				col = lerp(col, albedo.xyz, albedo.w * reflAlpha);
				#endif

				// Fog
				if (!underwater)
				{
					// Above water - do atmospheric fog.
				}
#if CREST_UNDERWATER_BEFORE_TRANSPARENT
				else
				{
					// underwater - do depth fog
					col = lerp(col, scatterCol, saturate(1.0 - exp(-_DepthFogDensity.xyz * pixelZ)));
				}
#endif

				return real4(col, 1.0);
			}

			ENDHLSL
		}
	}

	// If the above doesn't work then error.
	FallBack "Hidden/InternalErrorShader"
}
