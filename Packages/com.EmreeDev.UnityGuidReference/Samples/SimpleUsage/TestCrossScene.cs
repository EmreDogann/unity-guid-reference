using UnityEngine;
using UnityEngine.Profiling;

public class TestCrossScene : MonoBehaviour
{
    public GuidReference gameObjectRef = new GuidReference();
    public GuidReference<MeshRenderer> rendererRef = new GuidReference<MeshRenderer>();

    private void Update()
    {
        // Simple example looking for our reference and spinning both if we get one.
        // due to caching, this only causes a dictionary lookup the first time we call it, so you can comfortably poll.
        if (gameObjectRef.GameObject)
        {
            gameObjectRef.GameObject.transform.Rotate(Vector3.up, 10.0f * Time.deltaTime, Space.World);
        }

        if (rendererRef.Component)
        {
            transform.Rotate(new Vector3(0, 1, 0), 10.0f * Time.deltaTime);
            rendererRef.Component.gameObject.transform.Rotate(new Vector3(0, 1, 0), 10.0f * Time.deltaTime,
                Space.World);
        }

        // Added a performance test if you want to see. Most cost is in the profiling tags.
        // TestPerformance();
    }

    private void TestPerformance()
    {
        GameObject derefTest = null;

        for (int i = 0; i < 10000; ++i)
        {
            Profiler.BeginSample("Guid Resolution");
            derefTest = gameObjectRef.GameObject;
            Profiler.EndSample();
        }
    }
}