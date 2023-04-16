#if CLIENT
using JetBrains.Annotations;
using System.Globalization;

namespace DevkitServer.Players;
public class UserTPVControl : MonoBehaviour
{
    private static GameObject? _objectPrefab;
    private static bool _init;
    public EditorUser User { get; internal set; } = null!;
    public GameObject Model { get; private set; }

    public void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.LogError("Invalid UserTPVControl setup; EditorUser not found!");
            return;
        }
        if (!_init)
        {
            _init = true;
            if (DevkitServerModule.Bundle == null)
            {
                Logger.LogError("Unable to set up UserTPVControl object, " + "devkitserver.masterbundle".Format() + " not loaded.");
                return;
            }

            _objectPrefab = DevkitServerModule.Bundle.load<GameObject>("resources/tpv_char_server");
        }

        if (_objectPrefab == null)
        {
            Logger.LogError("Unable to set up UserTPVControl object, " + "Resources/TPV_Char_Server".Format() + " not found in " + "devkitserver.masterbundle".Format() + " (or it was not loaded).");
            return;
        }

        Model = Instantiate(_objectPrefab, transform, false);
        Model.name = "TPV_Editor_" + User.SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture);
    }

    [UsedImplicitly]
    void OnDestroy()
    {
        Destroy(Model);
        Model = null!;
    }

    internal static void Init()
    {
        Provider.onClientDisconnected += OnClientDisconnected;
    }
    internal static void Deinit()
    {
        Provider.onClientDisconnected -= OnClientDisconnected;
    }
    private static void OnClientDisconnected()
    {
        _init = false;
        Destroy(_objectPrefab);
        _objectPrefab = null;
    }
}

#endif