using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal static class CustomEditorElement
{
    // ---- EditorElement ----
    private static Func<EditorElement, Editor[], Editor[]> _populateCacheMethod;
    private static Func<EditorElement, bool> _isEditorValidMethod;
    private static Func<EditorElement, VisualElement, bool> _isElementVisibleMethod;
    private static Action<EditorElement> _updateInspectorVisibilityMethod;

    private static Func<EditorElement, Editor> _editorPropertyGetter;
    private static Func<EditorElement, Color> _playModeTintColorPropertyGetter;

    private static Func<EditorElement, InspectorElement> _inspectorElementGetter;
    private static Func<EditorElement, VisualElement> _decoratorElementGetter;
    private static Func<EditorElement, bool> _lastOpenForEditGetter;
    private static Action<EditorElement, bool> _lastOpenForEditSetter;

    private static Func<EditorElement, bool> _wasVisibleGetter;
    private static Action<EditorElement, bool> _wasVisibleSetter;

    private static Func<EditorElement, IPropertyView> _inspectorWindowGetter;
    private static Func<EditorElement, int> _editorIndexGetter;

    private static Action<EditorElement, Rect> _dragRectSetter;
    private static Action<EditorElement, Rect> _contentRectSetter;

    private static Func<EditorElement, IMGUIContainer> _headerGetter;

    // ---- EditorGUI ----

    private static Func<int> _titlebarHashGetter;
    private static Func<int, bool> _hasKeyFocusMethod;
    private static Func<Rect, GUIStyle, Rect> _getIconRectMethod;
    private static Func<Rect, GUIStyle, GUIStyle, Rect> _getSettingsRectMethod;
    private static Func<Rect, Rect, Rect, GUIStyle, GUIStyle, Rect> _getTextRectMethod;
    private static Action<bool, Rect, int> _doObjectFoldoutInternalMethod;

    public static Texture2D prefabOverlayAddedIcon = EditorGUIUtility.LoadIcon("PrefabOverlayAdded Icon");

    private static bool _isReflectionCacheInitialized;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        _isReflectionCacheInitialized = false;

        const BindingFlags privateInstanceFlag = BindingFlags.NonPublic | BindingFlags.Instance;
        Type editorElementType = typeof(EditorElement);

        MethodInfo populateCacheMethodInfo =
            editorElementType.GetMethod("PopulateCache", privateInstanceFlag);
        _populateCacheMethod =
            (Func<EditorElement, Editor[], Editor[]>)populateCacheMethodInfo.CreateDelegate(
                typeof(Func<EditorElement, Editor[], Editor[]>));

        MethodInfo isEditorValidMethodInfo =
            editorElementType.GetMethod("IsEditorValid", privateInstanceFlag);
        _isEditorValidMethod =
            (Func<EditorElement, bool>)isEditorValidMethodInfo.CreateDelegate(
                typeof(Func<EditorElement, bool>));

        MethodInfo isElementVisibleMethodInfo =
            editorElementType.GetMethod("IsElementVisible", privateInstanceFlag);
        _isElementVisibleMethod =
            (Func<EditorElement, VisualElement, bool>)isElementVisibleMethodInfo.CreateDelegate(
                typeof(Func<EditorElement, VisualElement, bool>));

        MethodInfo updateInspectorVisibilityMethodInfo =
            editorElementType.GetMethod("UpdateInspectorVisibility", privateInstanceFlag);
        _updateInspectorVisibilityMethod =
            (Action<EditorElement>)updateInspectorVisibilityMethodInfo.CreateDelegate(
                typeof(Action<EditorElement>));

        MethodInfo _editorPropertyInfo =
            editorElementType.GetProperty("editor", BindingFlags.Public | BindingFlags.Instance)
                .GetGetMethod(false);
        _editorPropertyGetter =
            (Func<EditorElement, Editor>)_editorPropertyInfo.CreateDelegate(
                typeof(Func<EditorElement, Editor>));

        MethodInfo _playModeTintColorPropertyInfo =
            editorElementType.GetProperty("playModeTintColor", privateInstanceFlag).GetGetMethod(true);
        _playModeTintColorPropertyGetter =
            (Func<EditorElement, Color>)_playModeTintColorPropertyInfo.CreateDelegate(
                typeof(Func<EditorElement, Color>));

        _inspectorElementGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, InspectorElement>("m_InspectorElement",
                privateInstanceFlag);
        _decoratorElementGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, VisualElement>("m_DecoratorsElement",
                privateInstanceFlag);

        _lastOpenForEditGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, bool>("m_LastOpenForEdit",
                privateInstanceFlag);
        _lastOpenForEditSetter =
            ReflectionUtility.BuildFieldSetter<EditorElement, bool>("m_LastOpenForEdit",
                privateInstanceFlag);

        _wasVisibleGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, bool>("m_WasVisible", privateInstanceFlag);
        _wasVisibleSetter =
            ReflectionUtility.BuildFieldSetter<EditorElement, bool>("m_WasVisible", privateInstanceFlag);

        _inspectorWindowGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, IPropertyView>("inspectorWindow",
                privateInstanceFlag);

        _editorIndexGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, int>("m_EditorIndex", privateInstanceFlag);

        _dragRectSetter =
            ReflectionUtility.BuildFieldSetter<EditorElement, Rect>("m_DragRect", privateInstanceFlag);

        _contentRectSetter =
            ReflectionUtility.BuildFieldSetter<EditorElement, Rect>("m_ContentRect", privateInstanceFlag);

        _headerGetter =
            ReflectionUtility.BuildFieldGetter<EditorElement, IMGUIContainer>("m_Header",
                privateInstanceFlag);

        // ---- EditorGUI ----
        const BindingFlags privateStaticFlag = BindingFlags.NonPublic | BindingFlags.Static;
        _titlebarHashGetter =
            ReflectionUtility.BuildFieldGetterStatic<EditorGUI, int>("s_TitlebarHash", privateStaticFlag);

        MethodInfo hasKeyFocusMethodInfo = typeof(GUIUtility).GetMethod("HasKeyFocus", privateStaticFlag);
        _hasKeyFocusMethod = (Func<int, bool>)hasKeyFocusMethodInfo.CreateDelegate(typeof(Func<int, bool>));

        MethodInfo iconRectMethodInfo = typeof(EditorGUI).GetMethod("GetIconRect", privateStaticFlag);
        _getIconRectMethod =
            (Func<Rect, GUIStyle, Rect>)iconRectMethodInfo.CreateDelegate(typeof(Func<Rect, GUIStyle, Rect>));

        MethodInfo settingsRectMethodInfo = typeof(EditorGUI).GetMethod("GetSettingsRect", privateStaticFlag);
        _getSettingsRectMethod =
            (Func<Rect, GUIStyle, GUIStyle, Rect>)settingsRectMethodInfo.CreateDelegate(
                typeof(Func<Rect, GUIStyle, GUIStyle, Rect>));

        MethodInfo textRectMethodInfo = typeof(EditorGUI).GetMethod("GetTextRect", privateStaticFlag);
        _getTextRectMethod =
            (Func<Rect, Rect, Rect, GUIStyle, GUIStyle, Rect>)textRectMethodInfo.CreateDelegate(
                typeof(Func<Rect, Rect, Rect, GUIStyle, GUIStyle, Rect>));

        MethodInfo doObjectFoldoutInternalMethodInfo =
            typeof(EditorGUI).GetMethod("DoObjectFoldoutInternal", privateStaticFlag);
        _doObjectFoldoutInternalMethod =
            (Action<bool, Rect, int>)doObjectFoldoutInternalMethodInfo.CreateDelegate(typeof(Action<bool, Rect, int>));

        _isReflectionCacheInitialized = true;
    }

    public static void CustomOnHeaderGUI(VisualElement element)
    {
        if (element is not EditorElement instance || !_isReflectionCacheInitialized)
        {
            return;
        }

        var editors = _populateCacheMethod(instance, null);
        if (!_isEditorValidMethod(instance))
        {
            if (_inspectorElementGetter(instance) != null)
            {
                EditorElement.SetElementVisible(_inspectorElementGetter(instance), false);
            }

            if (_decoratorElementGetter(instance) == null)
            {
                return;
            }

            EditorElement.SetElementVisible(_decoratorElementGetter(instance), false);
        }
        else
        {
            Object target = _editorPropertyGetter(instance).target;
            if (target == null && !NativeClassExtensionUtilities.ExtendsANativeType(target))
            {
                if (_inspectorElementGetter(instance) != null)
                {
                    EditorElement.SetElementVisible(_inspectorElementGetter(instance), false);
                }

                if (_decoratorElementGetter(instance) == null)
                {
                    return;
                }

                EditorElement.SetElementVisible(_decoratorElementGetter(instance), false);
            }
            else
            {
                if (_editorPropertyGetter(instance) != null)
                {
                    bool flag = _editorPropertyGetter(instance).IsOpenForEdit();
                    if (flag != _lastOpenForEditGetter(instance))
                    {
                        _lastOpenForEditSetter(instance, flag);
                        _inspectorElementGetter(instance)?.SetEnabled(flag);
                    }
                }

                _wasVisibleSetter(instance,
                    _inspectorWindowGetter(instance).WasEditorVisible(editors, _editorIndexGetter(instance), target));
                GUIUtility.GetControlID(target.GetInstanceID(), FocusType.Passive);
                EditorGUIUtility.ResetGUIState();
                GUI.color = _playModeTintColorPropertyGetter(instance);
                if (_editorPropertyGetter(instance).target is AssetImporter)
                {
                    _inspectorWindowGetter(instance).editorsWithImportedObjectLabel
                        .Add(_editorIndexGetter(instance) + 1);
                }

                ScriptAttributeUtility.propertyHandlerCache = _editorPropertyGetter(instance).propertyHandlerCache;
                using (new InspectorWindowUtils.LayoutGroupChecker())
                {
                    _dragRectSetter(instance,
                        CustomDrawEditorSmallHeader(instance, target, _wasVisibleGetter(instance)));
                }

                if (GUI.changed)
                {
                    EditorElement.InvalidateIMGUILayouts(instance);
                }

                if (_inspectorElementGetter(instance) != null &&
                    _wasVisibleGetter(instance) != _isElementVisibleMethod(instance, _inspectorElementGetter(instance)))
                {
                    EditorElement.SetElementVisible(_inspectorElementGetter(instance), _wasVisibleGetter(instance));
                }

                if (_decoratorElementGetter(instance) != null &&
                    _wasVisibleGetter(instance) != _isElementVisibleMethod(instance, _decoratorElementGetter(instance)))
                {
                    EditorElement.SetElementVisible(_decoratorElementGetter(instance), _wasVisibleGetter(instance));
                }

                _updateInspectorVisibilityMethod(instance);
                if (!PropertyEditor.IsMultiEditingSupported(_editorPropertyGetter(instance), target,
                        _inspectorWindowGetter(instance).inspectorMode) &&
                    _wasVisibleGetter(instance))
                {
                    GUILayout.Label("Multi-object editing not supported.", EditorStyles.helpBox);
                }
                else
                {
                    InspectorWindowUtils.DisplayDeprecationMessageIfNecessary(_editorPropertyGetter(instance));
                    if (Event.current.type == EventType.Repaint)
                    {
                        _editorPropertyGetter(instance).isInspectorDirty = false;
                    }

                    if (_editorPropertyGetter(instance).target != null && InspectorWindowUtils.IsExcludedClass(target))
                    {
                        EditorGUILayout.HelpBox(
                            "The module which implements this component type has been force excluded in player settings. This object will be removed in play mode and from any builds you make.",
                            MessageType.Warning);
                    }

                    if (_wasVisibleGetter(instance))
                    {
                        InspectorElement inspectorElement = _inspectorElementGetter(instance);
                        _contentRectSetter(instance, inspectorElement != null ? inspectorElement.layout : Rect.zero);
                    }
                    else
                    {
                        Rect layout = _headerGetter(instance).layout;
                        layout.y = (float)(layout.y + (double)layout.height - 1.0);
                        layout.height = 5f;
                        _contentRectSetter(instance, layout);
                    }
                }
            }
        }
    }

    private static Rect CustomDrawEditorSmallHeader(EditorElement instance, Object target, bool wasVisible)
    {
        Editor editor = _editorPropertyGetter(instance);
        if (editor == null)
        {
            return GUILayoutUtility.GetLastRect();
        }

        using (new EditorGUI.DisabledScope(!editor.IsEnabled()))
        {
            bool isExpanded = InspectorTitlebar(
                GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.inspectorTitlebar), wasVisible, editor);
            if (wasVisible != isExpanded)
            {
                _inspectorWindowGetter(instance).tracker.SetVisible(_editorIndexGetter(instance), isExpanded ? 1 : 0);
                InternalEditorUtility.SetIsInspectorExpanded(target, isExpanded);
                if (isExpanded)
                {
                    _inspectorWindowGetter(instance).lastInteractedEditor = editor;
                }
                else if (_inspectorWindowGetter(instance).lastInteractedEditor == editor)
                {
                    _inspectorWindowGetter(instance).lastInteractedEditor = null;
                }
            }
        }

        return GUILayoutUtility.GetLastRect();
    }

    public static bool InspectorTitlebar(Rect position, bool foldout, Editor editor)
    {
        GUIStyle inspectorTitlebar = EditorStyles.inspectorTitlebar;
        int controlId = GUIUtility.GetControlID(_titlebarHashGetter(), FocusType.Keyboard, position);
        DoInspectorTitlebar(position, controlId, foldout, editor.targets, editor.enabledProperty,
            inspectorTitlebar);
        foldout = EditorGUI.DoObjectMouseInteraction(foldout, position, editor.targets, controlId);
        if (editor.CanBeExpandedViaAFoldout())
        {
            Rect foldoutRenderRect = EditorGUI.GetInspectorTitleBarObjectFoldoutRenderRect(position, inspectorTitlebar);
            _doObjectFoldoutInternalMethod(foldout, foldoutRenderRect, controlId);
        }

        return foldout;
    }

    private static void DoInspectorTitlebar(
        Rect position,
        int id,
        bool foldout,
        Object[] targetObjs,
        SerializedProperty enabledProperty,
        GUIStyle baseStyle)
    {
        GUIStyle inspectorTitlebarText = EditorStyles.inspectorTitlebarText;
        GUIStyle iconButton = EditorStyles.iconButton;
        Event current = Event.current;
        bool isActive = GUIUtility.hotControl == id;
        bool hasKeyboardFocus = _hasKeyFocusMethod(id);
        bool isHover = position.Contains(current.mousePosition);
        Rect iconRect = _getIconRectMethod(position, baseStyle);
        Rect settingsRect = _getSettingsRectMethod(position, baseStyle, iconButton);
        Rect textRect = _getTextRectMethod(position, iconRect, settingsRect, baseStyle, inspectorTitlebarText);
        if (current.type == EventType.Repaint)
        {
            baseStyle.Draw(position, GUIContent.none, isHover, isActive, foldout, hasKeyboardFocus);
        }

        bool flag1 = false;
        Component targetObj1 = targetObjs[0] as Component;
        if (EditorGUI.ShouldDrawOverrideBackground(targetObjs, current, targetObj1))
        {
            flag1 = true;
            EditorGUI.DrawOverrideBackgroundApplicable(position, true);
        }

        int num = -1;
        foreach (Object targetObj2 in targetObjs)
        {
            if (targetObj1 is MonoBehaviour monoBehaviour)
            {
                // num = monoBehaviour.enabled ? 1 : 0;
            }
            else
            {
                int objectEnabled = EditorUtility.GetObjectEnabled(targetObj2);
                if (num == -1)
                {
                    if (EditorGUI.EnableCheckBoxInTitlebar(targetObj2))
                    {
                        num = objectEnabled;
                    }
                }
                else if (num != objectEnabled)
                {
                    num = -2;
                    break;
                }
            }
        }

        if (num != -1)
        {
            Rect rect1 = iconRect;
            rect1.x = iconRect.xMax + 4f;
            Rect position1 = rect1;

            Rect rect = new Rect(iconRect.x, iconRect.y, position1.xMax - iconRect.xMin, iconRect.height);
            if (enabledProperty != null)
            {
                enabledProperty.serializedObject.Update();
                EditorGUI.PropertyField(position1, enabledProperty, GUIContent.none);
                enabledProperty.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                bool flag2 = num != 0;
                EditorGUI.showMixedValue = num == -2;
                EditorGUI.BeginChangeCheck();
                Color backgroundColor = GUI.backgroundColor;
                bool flag3 = AnimationMode.IsPropertyAnimated(targetObjs[0], "m_Enabled");
                if (flag3)
                {
                    Color color = AnimationMode.animatedPropertyColor;
                    if (AnimationMode.InAnimationRecording())
                    {
                        color = AnimationMode.recordedPropertyColor;
                    }
                    else if (AnimationMode.IsPropertyCandidate(targetObjs[0], "m_Enabled"))
                    {
                        color = AnimationMode.candidatePropertyColor;
                    }

                    color.a *= GUI.color.a;
                    GUI.backgroundColor = color;
                }

                int controlId = GUIUtility.GetControlID(_titlebarHashGetter(), FocusType.Keyboard, position);
                bool enabled = EditorGUIInternal.DoToggleForward(position1, controlId, flag2, GUIContent.none,
                    EditorStyles.toggle);
                if (flag3)
                {
                    GUI.backgroundColor = backgroundColor;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targetObjs,
                        $"{(enabled ? "Enable" : "Disable")} Component{(targetObjs.Length > 1 ? "s" : "")}");
                    foreach (Object targetObj3 in targetObjs)
                    {
                        EditorUtility.SetObjectEnabled(targetObj3, enabled);
                    }
                }

                EditorGUI.showMixedValue = false;
            }

            if (rect.Contains(Event.current.mousePosition) &&
                (current.type == EventType.MouseDown && current.button == 1 || current.type == EventType.ContextClick))
            {
                EditorGUI.DoPropertyContextMenu(new SerializedObject(targetObjs).FindProperty("m_Enabled"));
                current.Use();
            }
        }

        Rect rectangle = settingsRect;
        // rectangle.x -= 20f;
        // rectangle = EditorGUIUtility.DrawEditorHeaderItems(rectangle, targetObjs, 4f);
        // textRect.xMax = rectangle.xMin - 4f;
        if (current.type == EventType.Repaint)
        {
            Texture2D miniThumbnail = AssetPreview.GetMiniThumbnail(targetObjs[0]);
            // GUIStyle.none.Draw(iconRect, EditorGUIUtility.TempContent(miniThumbnail),
            GUIStyle.none.Draw(iconRect, EditorGUIUtility.IconContent("InspectorLock"),
                iconRect.Contains(Event.current.mousePosition), false, false, false);
            if (flag1)
            {
                GUIStyle.none.Draw(iconRect, EditorGUIUtility.TempContent(prefabOverlayAddedIcon),
                    false, false, false, false);
            }
        }

        bool enabled1 = GUI.enabled;
        GUI.enabled = true;
        switch (current.type)
        {
            case EventType.MouseDown:
                if (EditorGUIUtility.comparisonViewMode == EditorGUIUtility.ComparisonViewMode.None &&
                    settingsRect.Contains(current.mousePosition))
                {
                    EditorUtility.DisplayObjectContextMenu(settingsRect, targetObjs, 0);
                    current.Use();
                }

                break;
            case EventType.Repaint:
                inspectorTitlebarText.Draw(textRect,
                    EditorGUIUtility.TempContent(ObjectNames.GetInspectorTitle(targetObjs[0], targetObjs.Length > 1)),
                    isHover, isActive, foldout, hasKeyboardFocus);
                if (EditorGUIUtility.comparisonViewMode == EditorGUIUtility.ComparisonViewMode.None)
                {
                    EditorStyles.optionsButtonStyle.Draw(settingsRect, GUIContent.none, id, foldout,
                        settingsRect.Contains(Event.current.mousePosition));
                }

                break;
        }

        GUI.enabled = enabled1;
    }
}