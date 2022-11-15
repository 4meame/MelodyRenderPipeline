using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

//define how to use a custom attribute class, not allowed multiple usage, allow to be inherited
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ExecuteDuringEditModeAttribute : Attribute {
    [Flags]
    public enum Include {
        None,
        PrefabStage,
        BuildPipeline,
        All = PrefabStage | BuildPipeline,
    }

    public Include _including;

    public ExecuteDuringEditModeAttribute(Include including = Include.PrefabStage) {
        _including = including;
    }
}
