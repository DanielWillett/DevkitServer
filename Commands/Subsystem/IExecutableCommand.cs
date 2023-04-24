using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Players;

namespace DevkitServer.Commands.Subsystem;

/// <summary>
/// Represents a command ran by either a <see cref="EditorUser"/> or the console.
/// </summary>
/// <remarks>Can be synchronous or asynchronous, depending on the value of <see cref="Asynchronous"/>.</remarks>
public interface IExecutableCommand
{
    /// <summary>
    /// Run <see cref="ExecuteAsync"/> instead of <see cref="Execute"/>.
    /// </summary>
    bool Asynchronous { get; }

    /// <summary>
    /// Treat <see cref="Permissions"/> as a list of any permission the user must have instead of a list of all permissions a user must have.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    bool AnyPermissions { get; }

    /// <summary>
    /// Name of the command, for example "/home" would have the name "home".
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Command selection priority. All vanilla commands are 0. Higher is chosen first.
    /// </summary>
    /// <remarks>Changes in this value will not be reflected after registration.</remarks>
    int Priority { get; }

    /// <summary>
    /// List of aliases for the command. This is safe to add to or remove from at runtime.
    /// </summary>
    /// <remarks>This getter is called each time someone runs a command. It is not a good idea to point it to a new <see cref="List{string}"/> each time it's called.</remarks>
    IList<string> Aliases { get; }

    /// <summary>
    /// List of all permissions required to run the command. Change this behavior with <see cref="AnyPermissions"/>. This is safe to add to or remove from at runtime.
    /// </summary>
    /// <remarks>This getter is called each time someone runs a command. It is not a good idea to point it to a new <see cref="List{API.Permissions.Permission}"/> each time it's called.</remarks>
    IList<Permission> Permissions { get; }

    /// <summary>
    /// Plugin that implements this command. May be <see langword="null"/> if the command comes from <see cref="DevkitServer"/>.
    /// </summary>
    IDevkitServerPlugin? Plugin { get; set; }

    /// <summary>
    /// Runs the command synchronously. Not required to be implemented if <see cref="Asynchronous"/> is <see langword="true"/>.
    /// </summary>
    void Execute(CommandContext ctx);

    /// <summary>
    /// Runs the command asynchronously. Not required to be implemented if <see cref="Asynchronous"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>Execution of this method will start on the game thread.</remarks>
    Task ExecuteAsync(CommandContext ctx, CancellationToken token);

    /// <summary>
    /// Check if the <paramref name="user"/> has permission to run this command.
    /// </summary>
#if SERVER
    /// <param name="user">The user that executed the command, or <see langword="null"/> when ran by a console.</param>
#endif
    /// <returns><see langword="true"/> if the user has permission to run this command, otherwise <see langword="false"/>.</returns>
    bool CheckPermission(
#if SERVER
        EditorUser? user
#endif
        );
}

/// <summary>
/// When <see cref="IExecutableCommand.Asynchronous"/> is set to <see langword="true"/>, only allows one execution of this command to run at once.
/// </summary>
public interface ISynchronizedAsyncCommand : IExecutableCommand
{
    /// <summary>
    /// Semaphore used to synchronize access to the command.
    /// </summary>
    /// <remarks>This property will be set during registration.</remarks>
    SemaphoreSlim Semaphore { get; set; }
}

/// <summary>
/// Allows a command to specify it's own <see cref="Local"/> dictionary.
/// </summary>
public interface ILocalizedCommand : IExecutableCommand
{
    /// <summary>
    /// Custom translations for this command.
    /// </summary>
    /// <remarks>This property will be set during registration unless it already has a value.</remarks>
    Local Translations { get; set; }
}

/// <summary>
/// Allows a command to specify it's own localization file.
/// </summary>
public interface ICommandLocalizationFile : ILocalizedCommand
{
    /// <summary>
    /// Relative to your plugin's data folder.
    /// </summary>
    string TranslationsDirectory { get; }

    /// <summary>
    /// Default translations for this command. All keys must be unique.
    /// </summary>
    /// <remarks>This getter is only ran once during registration.</remarks>
    DatDictionary DefaultTranslations { get; }
}