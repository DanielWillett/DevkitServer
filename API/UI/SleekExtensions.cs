namespace DevkitServer.API.UI;
public static class SleekExtensions
{
    /// <summary>
    /// Sets the 'IsClickable' or 'IsInteractable' values for one of the following types: <see cref="ISleekButton"/>, <see cref="ISleekSlider"/>,
    /// <see cref="ISleekToggle"/>, <see cref="SleekButtonIcon"/>, <see cref="SleekButtonIconConfirm"/>, <see cref="SleekButtonState"/>.
    /// </summary>
    /// <param name="isClickable">Can the user interact with the element (change it's value or click it)?</param>
    /// <returns><see langword="true"/> unless the input is not a valid type.</returns>
    public static bool SetIsClickable(this ISleekElement element, bool isClickable)
    {
        if (element is ISleekButton btn1)
            btn1.IsClickable = isClickable;
        else if (element is ISleekSlider slider)
            slider.IsInteractable = isClickable;
        else if (element is ISleekToggle toggle)
            toggle.IsInteractable = isClickable;
        else if (element is SleekButtonIcon btn2)
            btn2.isClickable = isClickable;
        else if (element is SleekButtonIconConfirm btn3)
            btn3.isClickable = isClickable;
        else if (element is SleekButtonState btn4)
            btn4.isInteractable = isClickable;
        else return false;

        return true;
    }
}