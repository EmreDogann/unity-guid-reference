using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiSceneComponentsProvider : IObjectProvider
{
    private static readonly List<GameObject> ROOT_OBJECT_CACHE = new List<GameObject>();
    private static readonly List<Component> COMPONENT_RESULT_CACHE = new List<Component>();

    private readonly Type _type;
    private readonly bool _isTargetTypeGameObject;

    public MultiSceneComponentsProvider(Type componentType)
    {
        _type = componentType;
        _isTargetTypeGameObject = _type == typeof(GameObject);
    }

    public IEnumerator<LookupInfo> Lookup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene currentScene = SceneManager.GetSceneAt(i);
            if (!currentScene.isLoaded)
            {
                continue;
            }

            currentScene.GetRootGameObjects(ROOT_OBJECT_CACHE);
            foreach (GameObject rootGameObject in ROOT_OBJECT_CACHE)
            {
                COMPONENT_RESULT_CACHE.AddRange(rootGameObject.GetComponentsInChildren(typeof(GuidComponent), true));

                if (_isTargetTypeGameObject)
                {
                    foreach (Component guidComponent in COMPONENT_RESULT_CACHE)
                    {
                        yield return new LookupInfo
                        {
                            Obj = guidComponent.gameObject,
                            ContainingScene = guidComponent.gameObject.scene
                        };
                    }
                }
                else
                {
                    foreach (Component guidComponent in COMPONENT_RESULT_CACHE)
                    {
                        var components = guidComponent.GetComponents(_type);
                        if (components.Length == 0)
                        {
                            continue;
                        }

                        foreach (Component component in components)
                        {
                            yield return new LookupInfo
                            {
                                Obj = component,
                                ContainingScene = guidComponent.gameObject.scene
                            };
                        }
                    }
                }

                COMPONENT_RESULT_CACHE.Clear();
            }

            ROOT_OBJECT_CACHE.Clear();
        }
    }
}