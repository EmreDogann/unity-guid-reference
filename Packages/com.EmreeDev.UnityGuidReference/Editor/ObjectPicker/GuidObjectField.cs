using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

public class SelectedGuidObject
{
    public readonly GuidComponent GuidComponent;
    public readonly Guid ComponentRef;
    public readonly Component ComponentCache;
    public readonly Object originalObjectCache;
    public SelectedGuidObject() {}

    public SelectedGuidObject(Object originalObject, GuidComponent guidComponent, Guid guid, Component componentCache)
    {
        originalObjectCache = originalObject;
        GuidComponent = guidComponent;
        ComponentRef = guid;
        ComponentCache = componentCache;
    }

    public static bool operator ==(SelectedGuidObject selectedGuidObject, SelectedGuidObject otherSelectedGuidObject)
    {
        return selectedGuidObject.Equals(otherSelectedGuidObject);
    }

    public static bool operator !=(SelectedGuidObject selectedGuidObject, SelectedGuidObject otherSelectedGuidObject)
    {
        return !(selectedGuidObject == otherSelectedGuidObject);
    }

    protected bool Equals(SelectedGuidObject other)
    {
        return Equals(GuidComponent, other.GuidComponent) && Equals(ComponentRef, other.ComponentRef);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((SelectedGuidObject)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GuidComponent.GetGuid(), ComponentRef);
    }
}

public class GuidObjectField
{
    private readonly Type _referenceType;
    private readonly ObjectFieldDrawer _objectFieldDrawer;
    private MultiSceneGuidPickerDropdown _objectPicker;

    private SelectedGuidObject _currentSelectedObject;
    private bool _dragPerformed;

    public Action<SelectedGuidObject> OnObjectChanged;
    private readonly bool _isTargetTypeGameObject;

    public GuidObjectField(Type referenceType, IObjectProvider objectProvider)
    {
        _objectFieldDrawer = new ObjectFieldDrawer(CheckObjectType, referenceType);

        _referenceType = referenceType;
        _isTargetTypeGameObject = _referenceType == typeof(GameObject);
        _currentSelectedObject = new SelectedGuidObject();

        InitializePickerPopup(objectProvider);

        _objectFieldDrawer.OnObjectPickerRequested += OpenObjectPicker;
        _objectFieldDrawer.OnDragPerformed += () => _dragPerformed = true;
        _objectPicker.OnOptionPicked += OnOptionPicked;
    }

    public float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, false);
    }

    public void DrawField(Rect position, GUIContent label, Object activeObject, Object objectToDraw = null)
    {
        Object newReference = _objectFieldDrawer.Draw(position, label, activeObject, objectToDraw);
        ResolveObject(newReference);
    }

    private void ResolveObject(Object newObj)
    {
        if (newObj == _currentSelectedObject.originalObjectCache)
        {
            return;
        }

        Component component = null;
        GuidComponent guidComponent = null;
        Guid guid = Guid.Empty;
        if (newObj != null)
        {
            if (newObj is GameObject go)
            {
                guidComponent = go.GetComponent<GuidComponent>();
            }
            else
            {
                guidComponent = newObj as GuidComponent;
                if (guidComponent == null && newObj is Component comp)
                {
                    component = comp;
                    guidComponent = component.GetComponent<GuidComponent>();
                }
            }
        }

        if (guidComponent != null)
        {
            guid = component != null ? guidComponent.GetGuid(component) : guidComponent.GetGuid(_referenceType);
        }

        SelectedGuidObject selectedGuidObject = new SelectedGuidObject(newObj, guidComponent, guid, component);

        if (_dragPerformed)
        {
            _dragPerformed = false;

            if (selectedGuidObject.GuidComponent != null &&
                selectedGuidObject.GuidComponent.HasMultipleComponentsOf(_referenceType))
            {
                _objectPicker.Open(_objectFieldDrawer.FieldRect, selectedGuidObject.GuidComponent);
                return;
            }
        }

        if (selectedGuidObject != _currentSelectedObject)
        {
            OnObjectChanged?.Invoke(selectedGuidObject);
        }

        _currentSelectedObject = selectedGuidObject;
    }

    private void OnOptionPicked(Object newObj)
    {
        ResolveObject(newObj);
    }

    private void InitializePickerPopup(IObjectProvider objectProvider)
    {
        _objectPicker = new MultiSceneGuidPickerDropdown(
            objectProvider,
            $"Select [{ObjectNames.NicifyVariableName(_referenceType.Name)}]",
            new AdvancedDropdownState(),
            info => info.Obj.GetType() == _referenceType || info.Obj.GetType().IsSubclassOf(_referenceType),
            obj =>
            {
                if (!_isTargetTypeGameObject && obj is GuidComponent guidComponent)
                {
                    if (guidComponent.TryGetComponent(_referenceType, out Component component))
                    {
                        return component;
                    }
                }

                return obj;
            }
        );
    }

    private void OpenObjectPicker()
    {
        _objectPicker.Open(_objectFieldDrawer.FieldRect);
    }

    public bool CheckObjectType(Object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (obj is GameObject go)
        {
            bool hasGuidComponent = go.TryGetComponent(out GuidComponent _);
            if (!_isTargetTypeGameObject)
            {
                if (!hasGuidComponent || !go.TryGetComponent(_referenceType, out Component _))
                {
                    return false;
                }
            }
            else
            {
                if (!hasGuidComponent)
                {
                    return false;
                }
            }

            return true;
        }

        return obj is GuidComponent;
    }
}