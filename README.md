# Guid Based Reference

_Globaly Unique IDentifier (GUID) for Game Objects and Components_

![Unity-Guid-Reference-Banner](https://github.com/EmreDogann/unity-guid-reference/assets/48212096/61bd886e-487e-451d-a88d-8f75af2c573e)

## What is this?
This is a fork of the excellent plugin created by the ![Unity Spotlight Team](https://github.com/Unity-Technologies/guid-based-reference) which allows cross scene referencing of Game Objects and serializing of said references to disk.

## What's Changed?
- Along with Game Objects, you can now also reference any Component across scenes.
- Revamped editor property drawers for Game Object and Component Guid References for a better UI/UX.
- Added optional support for ![Sisus' Component Names](https://assetstore.unity.com/packages/tools/utilities/component-names-212478)
- All the while keeping the runtime overhead to a minimum (![as envisioned with the original plugin](https://www.youtube.com/watch?v=6lRzXqfMXRo)).

## How To Install
Simply download this repository as a .zip file and extract into your project!

## How To Use
> The included sample at `CrossSceneReference/Samples` demonstrates a basic usage.
>
> Simply load the `LoadFirst.unity` scene and press play to see the plugin in action! Use the `SceneLoader` game object to load `LoadSecond.unity`.
>
> You should see the `CrossSceneReferencer` object in LoadFirst find the `CrossSceneTarget` object in LoadSecond, and set both of them to start spinning.

Add a `GuidComponent` to any Game Object where you want to reference either itself or one of its components.

![Unity_dYFYT0BkiJ](https://github.com/EmreDogann/unity-guid-reference/assets/48212096/5a37bb4a-be2a-4abe-871e-5219d1cb8399)

In any code that needs to be able to reference objects by GUID, add a `GuidReference` field (for Game Objects) or a `GuidReference<Type>` field (for Components).

For Game Object Reference:
- `GuidReference.GameObject` will return the GameObject if it is loaded, otherwise null.

For Component Reference:
- `GuidReference.GameObject` will return the GameObject the Component is attachted to if it is loaded, otherwise null.
- `GuidReference.Component` will return the Component if it is loaded, otherwise null. The returned component will already be casted to the correct type, so you can directly use it like normal.

```C#
public class TestComponent : MonoBehaviour
{
    public GuidReference gameObjectRef;              // Reference Game Object
    public GuidReference<MeshRenderer> rendererRef;  // Reference Component (MeshRenderer)

    private void Update()
    {
        if (gameObjectRef.GameObject)
        {
            Vector3 localPosition = gameObjectRef.GameObject.transform.localPosition;
            // ...
        }

        if (rendererRef.Component)
        {
            rendererRef.Component.receiveGI = ReceiveGI.LightProbes;
            // ...
        }
    }
}
```

