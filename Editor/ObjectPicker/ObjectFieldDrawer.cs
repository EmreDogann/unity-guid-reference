#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ObjectFieldDrawer
{
    public event Action OnObjectPickerRequested;
    public event Action OnDragPerformed;
    public readonly Predicate<Object> IsObjectValidForField;
    private readonly Func<Object, GUIContent> _objectLabelGetter;
    private readonly Type fieldType;

    private static readonly Type MonoBehaviourType = typeof(MonoBehaviour);

    public Rect FieldRect { get; private set; }

    public static GUIContent DefaultObjectLabelGetter(Object obj, string typeName)
    {
        string label = null;
        switch (obj)
        {
            case Component component:
#if COMPONENT_NAMES
                label = $"{cachedComponent.GetName()} ({cachedComponent.gameObject.name})";
#else
                label = $"{component.GetType().Name}";
#endif
                break;
            case GameObject:
                label = obj.name;
                break;
        }

        return obj
            ? new GUIContent(label, AssetPreview.GetMiniThumbnail(obj))
            : new GUIContent($"None ({typeName})");
    }

    public ObjectFieldDrawer(Predicate<Object> isObjectValidForField, Func<Object, GUIContent> objectLabelGetter)
    {
        IsObjectValidForField = isObjectValidForField;
        _objectLabelGetter = objectLabelGetter;
    }

    public ObjectFieldDrawer(Predicate<Object> isObjectValidForField, Type fieldType)
    {
        this.fieldType = fieldType;
        IsObjectValidForField = isObjectValidForField;
        _objectLabelGetter = obj => DefaultObjectLabelGetter(obj, ObjectNames.NicifyVariableName(fieldType.Name));
    }

    public ObjectFieldDrawer(Predicate<Object> isObjectValidForField, string fieldTypeName)
    {
        IsObjectValidForField = isObjectValidForField;
        _objectLabelGetter = obj => DefaultObjectLabelGetter(obj, fieldTypeName);
    }

    public Object Draw(Rect position, GUIContent label, Object activeObject, Object objectToDraw = null)
    {
        if (!objectToDraw)
        {
            objectToDraw = activeObject;
        }

        GUI.SetNextControlName("ObjectField");
        Rect dropBoxRect = EditorGUI.PrefixLabel(position, label);

        Rect buttonRect = dropBoxRect;
        buttonRect.xMin = dropBoxRect.xMax - 19f;
        buttonRect.xMax = dropBoxRect.xMax;
        buttonRect = new RectOffset(0, -1, -1, -1).Add(buttonRect);

        FieldRect = dropBoxRect;

        // we have to manually handle the mouse down events cause GUI.Button eats them
        if (GUI.enabled)
        {
            Event ev = Event.current;

            if (dropBoxRect.Contains(ev.mousePosition))
            {
                if (ev.type == EventType.MouseDown && ev.button == 0)
                {
                    bool isMouseOverSelectButton = buttonRect.Contains(ev.mousePosition);

                    if (isMouseOverSelectButton)
                    {
                        Event.current.Use();
                        OnObjectPickerRequested?.Invoke();
                    }
                    else
                    {
                        Event.current.Use();
                        if (activeObject)
                        {
                            // Double click opens the asset in external app or changes selection to referenced object
                            if (Event.current.clickCount == 2)
                            {
                                AssetDatabase.OpenAsset(activeObject);
                                ev.Use();
                                GUIUtility.ExitGUI();
                            }
                            else
                            {
                                EditorGUIUtility.PingObject(GetPingableObject(activeObject));
                            }
                        }

                        GUI.FocusControl("ObjectField");
                    }
                }
                else if (HandleDragEvents(GetDraggedObjectIfValid(), ref activeObject))
                {
                    Event.current.Use();
                }
            }
            else if (position.Contains(ev.mousePosition) && ev.button == 0)
            {
                if (ev.type == EventType.MouseDown)
                {
                    Event.current.Use();
                    GUI.FocusControl("ObjectField");
                }
            }

            if (ev.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "ObjectField")
            {
                if (ev.keyCode == KeyCode.Backspace ||
                    ev.keyCode == KeyCode.Delete && (ev.modifiers & EventModifiers.Shift) == 0)
                {
                    activeObject = null;

                    GUI.changed = true;
                    ev.Use();
                }
            }
        }

        GUIContent objectToDrawLabel = _objectLabelGetter(objectToDraw);

        GUI.SetNextControlName("ObjectField");
        GUI.Toggle(dropBoxRect, dropBoxRect.Contains(Event.current.mousePosition) && GetDraggedObjectIfValid(),
            GUIContent.none, EditorStyles.objectField);

        Rect iconRect = dropBoxRect;
        iconRect.center += Vector2.right * 3f;
        iconRect.width = 12f;

        Rect labelRect = dropBoxRect;
        labelRect.xMin += iconRect.width + 2f;

        GUIStyle labelStyle = new GUIStyle(EditorStyles.objectField);
        labelStyle.normal.background = Texture2D.blackTexture;

        Texture icon = objectToDrawLabel.image
            ? objectToDrawLabel.image
            : EditorGUIUtility.ObjectContent(null, fieldType).image;
        objectToDrawLabel.image = null;

        if (!icon && IsSameOrSubclass(MonoBehaviourType, fieldType))
        {
            icon = EditorGUIUtility.ObjectContent(null, typeof(MonoScript)).image;
        }

        EditorGUI.LabelField(labelRect, objectToDrawLabel, labelStyle);
        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

        GUIStyle objectFieldButtonStyle = new GUIStyle("ObjectFieldButton");
        GUI.Button(buttonRect, new GUIContent(""), objectFieldButtonStyle);

        return activeObject;
    }

    private Object GetPingableObject(Object activeObject)
    {
        if (activeObject is Component component)
        {
            return component.gameObject;
        }

        return activeObject;
    }

    private Object GetDraggedObjectIfValid()
    {
        var draggedObjects = DragAndDrop.objectReferences;
        if (draggedObjects.Length != 1)
        {
            return null;
        }

        Object obj = draggedObjects[0];

        return IsObjectValidForField.Invoke(obj) ? obj : null;
    }

    private bool HandleDragEvents(bool isValidObjectBeingDragged, ref Object activeObject)
    {
        Event ev = Event.current;
        if (ev.type == EventType.DragUpdated)
        {
            if (isValidObjectBeingDragged)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }

            return true;
        }

        if (ev.type == EventType.DragPerform)
        {
            if (isValidObjectBeingDragged)
            {
                DragAndDrop.AcceptDrag();
                activeObject = DragAndDrop.objectReferences[0];

                OnDragPerformed?.Invoke();
            }

            return true;
        }

        if (ev.type == EventType.DragExited)
        {
            return true;
        }

        return false;
    }

    bool IsSameOrSubclass(Type potentialBase, Type potentialDescendant)
    {
        return potentialDescendant.IsSubclassOf(potentialBase)
               || potentialDescendant == potentialBase;
    }

}