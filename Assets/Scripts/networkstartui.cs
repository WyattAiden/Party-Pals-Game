using UnityEngine;
using Unity.Netcode;
public class networkstartui : MonoBehaviour
{
    private void OnGUI()
    {
        float w = 200f, h = 40f;
        float x = 10f, y = 10f;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(x, y, w, h), "Start Host"))
            {
                NetworkManager.Singleton.StartHost();
            }
            y += h + 10f;
            if (GUI.Button(new Rect(x, y, w, h), "Start Client"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }


    }

}
