using UnityEngine.UIElements;

public class CustomLabelField : BaseField<string>
{
    // Exposed for customization
    private readonly VisualElement ContentElement;

    public CustomLabelField(string labelText = "")
        : base(labelText, new VisualElement())
    {
        focusable = true;
        delegatesFocus = true;
        ContentElement = this.Q<VisualElement>(className: inputUssClassName);
        // Ensure horizontal layout for content
        ContentElement.style.flexDirection = FlexDirection.Row;

        // Match PropertyField spacing exactly
        AddToClassList(alignedFieldUssClassName);

        StyleSheetUtility.ApplyCurrentTheme(this);
    }

    /// <summary>
    ///     Replace the label area with custom UI.
    /// </summary>
    public void SetCustomLabel(VisualElement customLabel, bool shouldClear = false)
    {
        labelElement.Clear();
        labelElement.Add(customLabel);
    }

    /// <summary>
    ///     Replace the content area with custom UI.
    /// </summary>
    public void SetCustomContent(VisualElement customContent)
    {
        ContentElement.Clear();
        ContentElement.Add(customContent);
    }
}