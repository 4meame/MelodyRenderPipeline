// Crest Ocean System

// Copyright 2022 Wave Harmonic Ltd

#ifndef BUILTIN_PIPELINE_LIGHTING_INCLUDED
#define BUILTIN_PIPELINE_LIGHTING_INCLUDED

// Abstraction over Light shading data.
struct Light
{
    half3   direction;
    half3   color;
    // half    distanceAttenuation;
    // half    shadowAttenuation;
};

Light GetMainLight()
{
    Light light;
    light.direction = half3(_WorldSpaceLightPos0.xyz);
    light.color = _LightColor0.rgb;
    return light;
}

#endif // BUILTIN_PIPELINE_LIGHTING_INCLUDED
