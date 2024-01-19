namespace DevkitServer.Util;

internal class CachedTime : MonoBehaviour
{
    private static readonly CachedMulticastEvent<Action> EventOnLateUpdate = new CachedMulticastEvent<Action>(typeof(CachedTime), nameof(OnLateUpdate));
    internal static CachedTime? Instance { get; private set; }
    private static float _deltaTime;
    private static bool _deltaTimeSet;
    private static float _realtimeSinceStartup;
    private static bool _realtimeSinceStartupSet;

    public static event Action OnLateUpdate
    {
        add => EventOnLateUpdate.Add(value);
        remove => EventOnLateUpdate.Remove(value);
    }

    public static float DeltaTime
    {
        get
        {
            if (!_deltaTimeSet)
            {
                _deltaTimeSet = true;
                _deltaTime = Time.deltaTime;
            }

            return _deltaTime;
        }
    }

    public static float RealtimeSinceStartup
    {
        get
        {
            if (!_realtimeSinceStartupSet)
            {
                _realtimeSinceStartupSet = true;
                _realtimeSinceStartup = Time.realtimeSinceStartup;
            }

            return _realtimeSinceStartup;
        }
    }

    [UsedImplicitly]
    private void Awake()
    {
        if (Instance != null)
            Destroy(Instance);
        
        Instance = this;
    }

    [UsedImplicitly]
    private void Update()
    {
        _deltaTimeSet = false;
        _realtimeSinceStartupSet = false;
    }

    [UsedImplicitly]
    private void LateUpdate()
    {
        EventOnLateUpdate.TryInvoke();
    }
}
