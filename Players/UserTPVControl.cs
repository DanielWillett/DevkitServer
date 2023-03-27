#if CLIENT
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Players;
public class UserTPVControl : MonoBehaviour
{
    public EditorUser User { get; private set; } = null!;
    public GameObject _host;

    public void Start()
    {
        if (!TryGetComponent(out EditorUser u))
        {
            Destroy(this);
            Logger.LogError("Invalid UserTPVControl setup; EditorUser not found!");
            return;
        }

        User = u;

        _host = new GameObject("TPV_Editor_" + u.SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture));
        MeshFilter filter = _host.AddComponent<MeshFilter>();
        
        // cube
        const float size = 1f;
        filter.mesh = new Mesh
        {
            vertices = new Vector3[]
            {
                new Vector3(size, size, -size),
                new Vector3(-size, -size, -size),
                new Vector3(-size, size, size),
                new Vector3(size, size, size),
                new Vector3(size, -size, size),
                new Vector3(-size, -size, size),
                new Vector3(-size, -size, -size),
                new Vector3(size, -size, -size),
            },
            triangles = new int[]
            {
                0, 1, 2, 0, 3, 2, 3, 4, 5, 3, 2, 5, 3, 4, 7, 3, 0, 7, 1, 2, 6, 2, 5, 6, 0, 1, 6, 7, 0, 1, 4, 5, 6, 7, 4, 6
            }
        };
        MeshRenderer renderer = _host.AddComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Diffuse")) { color = Color.red };
    }

    [UsedImplicitly]
    void OnDestroy()
    {
        Destroy(_host);
    }
}

#endif