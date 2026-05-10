using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

public class EditorStepForwardToolbarButton : MonoBehaviour
{
    private const string kElementPath = "Test Runner/Step Forward";
    private static bool _stepForward;
    private static int _totalStepCount;
    private static int _currentStepCount;
    private static bool _shouldShow;

    internal static bool ConsumeStep()
    {
        bool stepForward = _stepForward;
        if (stepForward)
        {
            _stepForward = false;
            _currentStepCount++;
        }

        return stepForward;
    }

    internal static void ShowButton(int totalSteps = 0)
    {
        _shouldShow = true;
        _totalStepCount = totalSteps;
        MainToolbar.Refresh(kElementPath);
    }

    internal static void HideButton()
    {
        _shouldShow = false;
        _totalStepCount = 0;
        MainToolbar.Refresh(kElementPath);
    }

    [MainToolbarElement(kElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
    internal static MainToolbarElement CreateButton()
    {
        string buttonText;
        if (_totalStepCount > 0)
        {
            buttonText = $"Test: Step {_currentStepCount}/{_totalStepCount}";
        }
        else
        {
            buttonText = "Step Test";
        }

        var icon = EditorGUIUtility.IconContent("StepButton").image as Texture2D;
        var content = new MainToolbarContent(buttonText, icon, "Step Forward in the Editor when running a test.");

        MainToolbarButton mainToolbarElement = new MainToolbarButton(content, () => _stepForward = true);
        mainToolbarElement.displayed = _shouldShow;

        return mainToolbarElement;
    }
}