# Guid Based Reference

_Globaly Unique IDentifier (GUID) for Game Objects and Components_

![Unity-Guid-Reference-Banner](https://github.com/EmreDogann/unity-guid-reference/assets/48212096/61bd886e-487e-451d-a88d-8f75af2c573e)

## What is this?
This is a fork of the excellent plugin created by the [Unity Spotlight Team](https://github.com/Unity-Technologies/guid-based-reference) which allows cross scene referencing of Game Objects and serializing of said references to disk.

If you want to learn more about this plugin, please see this fantastic [video](https://www.youtube.com/watch?v=6lRzXqfMXRo).

## What's Changed?
- Along with Game Objects, you can now also reference any Component across scenes.
- GuidComponent now better retains its Guids when used in prefabs (although not in all cases, please see Issue tickets).
- Revamped editor property drawers for Game Object and Component Guid References for a better UI/UX.
- Added optional support for [Sisus' Component Names](https://assetstore.unity.com/packages/tools/utilities/component-names-212478).
- All the while trying to keep the runtime performance overhead and garbage generation to a minimum.

## Installation
To install the package, go to **Package Manager -> Add package from git URL**, and add:
```
https://github.com/EmreDogann/unity-guid-reference.git
```

## Usage
> You can optionally install a sample showcasing basic usage of the package in the **Samples** tab of the package's description.
>
> In the sample, simply load the `LoadFirst.unity` scene and press play to see the plugin in action! Use the `SceneLoader` game object to load `LoadSecond.unity`.
>
> You should see the `CrossSceneReferencer` object in LoadFirst find the `CrossSceneTarget` object in LoadSecond, and set both of them to start spinning.

Add a `GuidComponent` to any Game Object where you want to reference either itself or one of its components.

![Unity_dYFYT0BkiJ](https://github.com/EmreDogann/unity-guid-reference/assets/48212096/5a37bb4a-be2a-4abe-871e-5219d1cb8399)

In any code that needs to be able to reference objects by GUID, add a `GuidReference` field (for Game Objects) or a `GuidReference<Type>` field (for Components).

For Game Object References:
- `GuidReference.GameObject` will return the GameObject if it is loaded, otherwise null.

For Component References:
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

## Bugs
This package is still a Work-In-Progress and has not been fully tested for all edge cases. If you encounter a bug, please create an issue on this repo and I will try my best to help you out! :)
