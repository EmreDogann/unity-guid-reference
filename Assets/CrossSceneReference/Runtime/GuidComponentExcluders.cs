using System;
using System.Collections.Generic;
using UnityEngine;

public static class GuidComponentExcluders
{
    public static List<Type> Excluders = new List<Type>
    {
        typeof(Transform),
        typeof(GuidComponent)
    };
}