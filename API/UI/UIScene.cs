#if CLIENT
namespace DevkitServer.API.UI;

/// <summary>
/// Represents a category for vanilla UI tyeps.
/// </summary>
public enum UIScene
{
    /// <summary>
    /// Other UI elements like sleek buttons, etc.
    /// </summary>
    Global,

    /// <summary>
    /// Child of <see cref="LoadingUI"/>
    /// </summary>
    Loading,

    /// <summary>
    /// Child of <see cref="MenuUI"/>
    /// </summary>
    Menu,

    /// <summary>
    /// Child of <see cref="PlayerUI"/>
    /// </summary>
    Player,

    /// <summary>
    /// Child of <see cref="EditorUI"/>
    /// </summary>
    Editor
}
#endif