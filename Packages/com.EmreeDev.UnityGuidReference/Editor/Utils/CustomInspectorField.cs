using UnityEngine.UIElements;

public class CustomLabelField : BaseField<string>
{
    // Exposed for customization
    private readonly VisualElement ContentElement;

    public CustomLabelField(string labelText)
        : base(labelText, new VisualElement())
    {
        ContentElement = this.Q<VisualElement>(className: inputUssClassName);
        // Ensure horizontal layout for content
        ContentElement.style.flexDirection = FlexDirection.Row;

        // Match PropertyField spacing exactly
        AddToClassList(alignedFieldUssClassName);

        // Add extra 1px to bottom margin, due to off-by-1 pixel offset compared to default fields like Object/Property fields.
        labelElement.style.marginBottom = 1;
    }

    /// <summary>
    ///     Replace the label area with custom UI.
    /// </summary>
    public void SetCustomLabel(VisualElement customLabel)
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