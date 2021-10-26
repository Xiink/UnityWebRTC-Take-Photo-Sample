using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine.UI;

public class NodeDssSingalerUI : MonoBehaviour
{
    public NodeDssSignaler NodeDssSignaler;

    public string RemotePeerId;

    /// <summary>
    /// The id of the <see cref="PlayerPrefs"/> key that we cache the last connected target id under
    /// </summary>
    private const string kLastRemotePeerId = "lastRemotePeerId";

    // Start is called before the first frame update
    void Start()
    {
        string localPeerId = NodeDssSignaler.LocalPeerId;
        if (!string.IsNullOrEmpty(NodeDssSignaler.RemotePeerId))
        {
            RemotePeerId = NodeDssSignaler.RemotePeerId;
        }
        else if (PlayerPrefs.HasKey(kLastRemotePeerId))
        {
            RemotePeerId = PlayerPrefs.GetString(kLastRemotePeerId);
        }
    }

    public void StartConnection() {
        if (!string.IsNullOrEmpty(RemotePeerId))
        {
            PlayerPrefs.SetString(kLastRemotePeerId, RemotePeerId);
            NodeDssSignaler.RemotePeerId = RemotePeerId;
            var success = NodeDssSignaler.PeerConnection.StartConnection();
            if (success)
            {
                Debug.Log("success connect");
            }
            else
            {
                Debug.Log("failed connect");
            }
        }
    }
}
