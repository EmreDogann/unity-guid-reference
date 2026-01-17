#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class MultiSceneGuidPickerDropdown : AdvancedDropdown
{
    private enum ViewState
    {
        Everything,
        ProvidedObject
    }

    public event Action<Object> OnOptionPicked;

    public string Title;

    private readonly List<Object> _objects = new List<Object>();
    private AdvancedDropdownItem _providedObjectRoot;
    private ViewState _viewState = ViewState.Everything;

    private readonly IObjectProvider _lookupStrategy;
    private readonly Predicate<LookupInfo> _filter;
    private readonly Func<Object, Object> _objectPreprocess;

    public MultiSceneGuidPickerDropdown(IObjectProvider lookupStrategy, string title, AdvancedDropdownState state,
        Predicate<LookupInfo> filter = null, Func<Object, Object> objectPreprocess = null) : base(state)
    {
        Title = title;
        _lookupStrategy = lookupStrategy;
        _filter = filter;
        _objectPreprocess = objectPreprocess;

        // Vector2 minSize = minimumSize;
        // minSize.x = 250.0f;
        // minSize.y = 300.0f;
        // minimumSize = minSize;
    }

    public void Open(Rect sourceRect, GuidComponent guidComponent = null)
    {
        _viewState = ViewState.Everything;
        if (guidComponent)
        {
            _viewState = ViewState.ProvidedObject;
            BuildOptions(guidComponent);
        }

        Show(sourceRect);
        SetMinMaxSizeForOpenedPopup(sourceRect, 400.0f, 300.0f, 400.0f, 400.0f);
    }

    private void SetMinMaxSizeForOpenedPopup(Rect rect, float minWidth, float minHeight, float maxWidth,
        float maxHeight)
    {
        EditorWindow window = EditorWindow.focusedWindow;

        if (!window)
        {
            Debug.LogWarning("EditorWindow.focusedWindow was null.");
            return;
        }

        if (!string.Equals(window.GetType().Namespace, typeof(AdvancedDropdown).Namespace))
        {
            Debug.LogWarning("EditorWindow.focusedWindow " + EditorWindow.focusedWindow.GetType().FullName +
                             " was not in expected namespace.");
            return;
        }

        Rect position = window.position;

        position.height = Mathf.Clamp(position.height, minHeight, maxHeight);
        position.width = Mathf.Clamp(position.width, minWidth, maxWidth);

        Vector2 minSize = new Vector2(minWidth, minHeight);
        window.minSize = minSize;
        window.maxSize = position.size;
        window.position = position;
        window.ShowAsDropDown(GUIUtility.GUIToScreenRect(rect), position.size);
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        base.ItemSelected(item);

        if (item.enabled)
        {
            if (item.id >= 0)
            {
                OnOptionPicked?.Invoke(_objects[item.id]);
            }
            else
            {
                OnOptionPicked?.Invoke(null);
            }
        }
    }

    private void BuildOptions(GuidComponent guidComponent)
    {
        AdvancedDropdownItem root = new AdvancedDropdownItem(Title);
        _objects.Clear();

        var iterator = _lookupStrategy.Lookup();
        while (iterator.MoveNext())
        {
            LookupInfo cur = iterator.Current;

            if (_filter != null && !_filter.Invoke(cur) && cur.Obj != guidComponent)
            {
                continue;
            }

            AdvancedDropdownItem item = null;
            Object obj = _objectPreprocess(cur.Obj);

            switch (obj)
            {
                case Component component:
                    if (component.GetComponent<GuidComponent>() != guidComponent)
                    {
                        continue;
                    }

                    string label = "";
#if COMPONENT_NAMES
                    label = $"{component.gameObject.name} ({component.GetName()})";
#else
                    label = $"{component.gameObject.name} ({component.GetType().Name})";
#endif

                    item = new AdvancedDropdownItem(label)
                    {
                        icon = AssetPreview.GetMiniThumbnail(obj),
                        id = _objects.Count
                    };
                    break;
                case GameObject:
                    item = new AdvancedDropdownItem(obj.name)
                    {
                        icon = AssetPreview.GetMiniThumbnail(obj),
                        id = _objects.Count
                    };
                    break;
            }

            root.AddChild(item);

            _objects.Add(cur.Obj);
        }

        _providedObjectRoot = root;
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        if (_viewState == ViewState.ProvidedObject)
        {
            return _providedObjectRoot;
        }

        AdvancedDropdownItem root = new AdvancedDropdownItem(Title);
        _objects.Clear();

        int sceneCount = 0;
        AdvancedDropdownItem scene = new AdvancedDropdownItem("Scene");

        AdvancedDropdownItem nullChoice = new AdvancedDropdownItem("None");
        nullChoice.id = -1;
        root.AddChild(nullChoice);

        var sceneToItemMap = new Dictionary<Scene, AdvancedDropdownItem>();
        var iterator = _lookupStrategy.Lookup();
        while (iterator.MoveNext())
        {
            LookupInfo cur = iterator.Current;
            AdvancedDropdownItem parentItem = root;

            if (_filter != null && !_filter.Invoke(cur))
            {
                continue;
            }

            if (sceneToItemMap.TryGetValue(cur.ContainingScene, out AdvancedDropdownItem sceneItem))
            {
                parentItem = sceneItem;
            }
            else
            {
                AdvancedDropdownItem sceneItemToAdd = new AdvancedDropdownItem(cur.ContainingScene.name + " (Scene)");
                sceneToItemMap.Add(cur.ContainingScene, sceneItemToAdd);
                root.AddChild(sceneItemToAdd);
                parentItem = sceneItemToAdd;
            }

            AdvancedDropdownItem item = null;
            Object obj = _objectPreprocess(cur.Obj);

            switch (obj)
            {
                case Component component:
                    string label = "";
#if COMPONENT_NAMES
                    label = $"{component.gameObject.name} ({component.GetName()})";
#else
                    label = $"{component.gameObject.name} ({component.GetType().Name})";
#endif

                    item = new AdvancedDropdownItem(label)
                    {
                        icon = AssetPreview.GetMiniThumbnail(obj),
                        id = _objects.Count
                    };
                    break;
                case GameObject:
                    item = new AdvancedDropdownItem(obj.name)
                    {
                        icon = AssetPreview.GetMiniThumbnail(obj),
                        id = _objects.Count
                    };
                    break;
            }

            sceneCount++;
            parentItem.AddChild(item);

            _objects.Add(cur.Obj);
        }

        scene.enabled = sceneCount != 0;
        return root;
    }
}