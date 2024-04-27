using UnityEditor;

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _componentGuidProp;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (_guidComp == null)
        {
            _guidComp = (GuidComponent)target;
        }

        _componentGuidProp = serializedObject.FindProperty("componentGUIDs");

        // Draw label
        EditorGUILayout.LabelField("GameObject GUID", _guidComp.GetGuid().ToString());

        if (_guidComp.GetComponentGUIDs().Count > 0)
        {
            _componentGuidProp.isExpanded = EditorGUILayout.Foldout(_componentGuidProp.isExpanded, "Component GUIDs");
        }

        if (_componentGuidProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (GuidComponent.ComponentGuid componentGuid in _guidComp.GetComponentGUIDs())
            {
                EditorGUILayout.LabelField($"{componentGuid.component.GetType().Name}:", componentGuid.Guid.ToString());
            }

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}