using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;
using System.Reflection;

/*
[Title("Noise 3D")]
public class Noise3DNode : CodeFunctionNode
{
    public Noise3DNode()
    {
        name = "Noise3DNode";
    }

    private static string Noise3DNodeFunction(
        [Slot(0, Binding.None)]Vector3 position,
        [Slot(1, Binding.None)] out Vector1 o
        )
    {
        return @"
    float3 p = floor(position);
    float3 f = frac(position);

    f       = f*f*(3.0-2.0*f);
    float n = p.x + p.y*57.0 + 113.0*p.z;

    o = lerp(lerp(lerp( hash(n+0.0), hash(n+1.0),f.x),
                   lerp( hash(n+57.0), hash(n+58.0),f.x),f.y),
               lerp(lerp( hash(n+113.0), hash(n+114.0),f.x),
                   lerp( hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);

        ";
    }

    public override void GenerateNodeFunction(FunctionRegistry registry, GenerationMode generationMode)
    {
        registry.ProvideFunction("hash", s => s.Append(@"
float hash( float n )
{
    return frac(sin(n)*43758.5453);
}
        "));

        base.GenerateNodeFunction(registry, generationMode);
    }

    protected override MethodInfo GetFunctionToConvert()
    {
        return GetType().GetMethod("Noise3DNodeFunction",
            BindingFlags.Static | BindingFlags.NonPublic);
    }
}
*/
