namespace DevkitServer.Util;
public static class InputUtil
{
    /// <summary>
    /// Is holding down the control key (or the command key on mac).
    /// </summary>
    /// <param name="left">Check the key on the left side of the keyboard.</param>
    /// <param name="right">Check the key on the right side of the keyboard.</param>
    public static bool IsHoldingControl(bool left = true, bool right = true)
    {
        if (Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer or
            RuntimePlatform.OSXServer or RuntimePlatform.IPhonePlayer or RuntimePlatform.tvOS)
        {
            return left && InputEx.GetKey(KeyCode.LeftCommand) || right && InputEx.GetKey(KeyCode.RightCommand);
        }

        return left && InputEx.GetKey(KeyCode.LeftControl) || right && InputEx.GetKey(KeyCode.RightControl);
    }
}
