# Guid Based Reference

_Globaly Unique IDentifier (GUID) for Game Objects and Components_

![Unity-Guid-Reference-Banner](https://github.com/EmreDogann/unity-guid-reference/assets/48212096/61bd886e-487e-451d-a88d-8f75af2c573e)

Maintainers
William Armstrong williama@unity3d.com

To Use:

Add a GuidComponent to any object you want to be able to reference.

In any code that needs to be able to reference objects by GUID, add a GuidReference field.

GuidReference.gameObject will then return the GameObject if it is loaded, otherwise null.

Look in the CrossSceneReference/SampleContent folder for example usage.

Load up the LoadFirst scene, and then use the SceneLoader object to load 'LoadSecond'

You should see the CrossSceneReferencer object find the CrossSceneTarget object, and set both of them to start spinning.

