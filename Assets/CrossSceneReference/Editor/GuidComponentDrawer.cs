using System.Linq;
using UnityEditor;
using UnityEngine;
#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _serializedGuidProp;
    private bool _isInit;


    private void Initialize()
    {
        if (!_isInit)
        {
            _guidComp = (GuidComponent)serializedObject.targetObject;

            _isInit = true;
        }
    }

    private void UpdateComponentGuids()
    {
        for (int i = _guidComp.ComponentGUIDs.Count - 1; i >= 0; i--)
        {
            if (!_guidComp.ComponentGUIDs[i].component)
            {
                GuidManager.Remove(_guidComp.ComponentGUIDs[i].Guid);
                _guidComp.ComponentGUIDs.RemoveAt(i);
            }
        }

        var components = _guidComp.gameObject.GetComponents<Component>()
            .Where(component => !GuidComponentExcluders.Excluders.Contains(component.GetType())).ToList();

        if (components.Count == 0)
        {
            _guidComp.ComponentGUIDs.Clear();
        }

        foreach (Component component in components)
        {
            ComponentGuid componentGuid =
                _guidComp.ComponentGUIDs.FirstOrDefault(c => c.component == component);
            if (componentGuid == null)
            {
                componentGuid = new ComponentGuid { component = component };
                _guidComp.ComponentGUIDs.Add(componentGuid);
            }

            _guidComp.CreateGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid,
                componentGuid.editorSerializedGuid);
            componentGuid.editorSerializedGuid = componentGuid.serializedGuid;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        _serializedGuidProp = serializedObject.FindProperty("_serializedGuid");
        Initialize();

        if (EditorUtility.IsDirty(_guidComp.gameObject))
        {
            UpdateComponentGuids();
            EditorUtility.ClearDirty(_guidComp.gameObject);
        }

        // Draw label
        EditorGUILayout.LabelField("GameObject GUID", _guidComp.GetGuid().ToString());

        if (_guidComp.GetComponentGUIDs().Count > 0)
        {
            _serializedGuidProp.isExpanded =
                EditorGUILayout.Foldout(_serializedGuidProp.isExpanded, "Component GUIDs", true);
            if (_serializedGuidProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (ComponentGuid componentGuid in _guidComp.GetComponentGUIDs())
                {
                    string label = "";
#if COMPONENT_NAMES
                    label = $"{componentGuid.component.GetName()}:";
#else
                    label = $"{componentGuid.component.GetType().Name}:";
#endif
                    EditorGUILayout.LabelField(label, componentGuid.Guid.ToString());
                }

                EditorGUI.indentLevel--;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}