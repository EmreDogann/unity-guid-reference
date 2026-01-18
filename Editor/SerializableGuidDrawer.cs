using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(SerializableGuid))]
public class SerializableGuidDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        TextField textField = new TextField(property.displayName)
        {
            isReadOnly = true
        };
        textField.AddToClassList(TextField.alignedFieldUssClassName);

        UpdateField(property);

        textField.TrackPropertyValue(property, UpdateField);
        textField.BindProperty(property);

        void UpdateField(SerializedProperty obj)
        {
            SerializedProperty low = obj.FindPropertyRelative("m_GuidLow");
            SerializedProperty high = obj.FindPropertyRelative("m_GuidHigh");

            if (low == null || high == null)
            {
                textField.value = "<Invalid SerializableGuid>";
                return;
            }

            Guid guid = new SerializableGuid(
                (ulong)low.longValue,
                (ulong)high.longValue
            ).Guid;

            textField.SetValueWithoutNotify(guid.ToString());
        }

        // Since we're modifying the input, we also need to update the serialized object.
        // property.stringValue = guid.ToString();
        // property.serializedObject.ApplyModifiedProperties();

        return textField;
    }
}