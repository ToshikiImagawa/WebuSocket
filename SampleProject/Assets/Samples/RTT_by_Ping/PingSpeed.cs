using UnityEngine;
using System.Collections;
using WebuSocketCore;

public class PingSpeed : MonoBehaviour {
	private WebuSocket webSocket;

	// Use this for initialization
	void Start () {
		webSocket = new WebuSocket(
			"wss://echo.websocket.org:443/",
			1024,
			() => {
				webSocket.Ping(
					pingRtt => {
						Debug.Log("websocket ping rtt:" + pingRtt);
						Debug.Log("websocket last ping rtt:" + webSocket.RttMilliseconds);
					}
				);
			},
			datas => {},
			() => {},
			closeReason => {
				Debug.Log("closed, closeReason:" + closeReason);
			}
		);
	}

	void OnApplicationQuit() {
		webSocket.Disconnect(true);
	}
}
