#if CLIENT
namespace DevkitServer.API.UI;
public static class SleekExtensions
{

    /// <summary>
    /// Sets the 'IsClickable' or 'IsInteractable' values for one of the following types: <see cref="SDG.Unturned.ISleekButton"/>, <see cref="SDG.Unturned.ISleekSlider"/>,
    /// <see cref="SDG.Unturned.ISleekToggle"/>, <see cref="SDG.Unturned.ISleekField"/>, <see cref="SDG.Unturned.ISleekNumericField"/>,
    /// <see cref="SleekButtonIcon"/>, <see cref="SleekButtonIconConfirm"/>, <see cref="SleekButtonState"/>.
    /// </summary>
    /// <param name="isClickable">Can the user interact with the element (change it's value or click it)?</param>
    /// <returns><see langword="true"/> unless the input is not a valid type.</returns>
    public static bool SetIsClickable(this ISleekElement element, bool isClickable)
    {
        switch (element)
        {
            case ISleekButton btn1:
                btn1.IsClickable = isClickable;
                return true;

            case ISleekSlider slider:
                slider.IsInteractable = isClickable;
                return true;

            case ISleekToggle toggle:
                toggle.IsInteractable = isClickable;
                return true;

            case ISleekField field:
                field.IsClickable = isClickable;
                return true;

            case ISleekNumericField field:
                field.IsClickable = isClickable;
                return true;

            case SleekButtonIcon btn2:
                btn2.isClickable = isClickable;
                return true;

            case SleekButtonIconConfirm btn3:
                btn3.isClickable = isClickable;
                return true;

            case SleekButtonState btn4:
                btn4.isInteractable = isClickable;
                return true;

            default:
                return false;
        }
    }
}
#endif