using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct LookupInfo
{
    public Object Obj;
    public Scene ContainingScene;
}
public interface IObjectProvider
{
    IEnumerator<LookupInfo> Lookup();
}