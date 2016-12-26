using System.Collections;

using UnityEngine;

using WebuSocketCore;


/**
	sample of websocket-disconnect then reconnect.
	repeat connect -> disconnect -> reconnect -> disconnect... 
*/
public class ReconnectionSampleScript : MonoBehaviour {

	WebuSocket webSocket;

	bool opened = false;

	void Start () {
		webSocket = new WebuSocket(
			"wss://echo.websocket.org:443/",
			1024,
			() => {
				Debug.Log("connection succeeded.");
				opened = true;
			},
			datas => {},
			() => {},
			closeReason => {
				Debug.Log("closed, closeReason:" + closeReason);
				switch (closeReason) {
					case WebuSocketCloseEnum.CLOSED_BY_TIMEOUT: {
						Debug.Log("start reconnect.");
						StartCoroutine(Reconnection(webSocket));
						break;
					}
				}
			}
		);
	}

	private IEnumerator Reconnection (WebuSocket ws) {
		yield return new WaitForSeconds(1);
		webSocket = WebuSocket.Reconnect(ws);
	}

	int frame = 0;
	void Update () {
		// disconnect after 2sec.
		if (opened) {
			if (frame == 120) {
				opened = false;
				frame = 0;
				// set timeout for sample.
				webSocket.Disconnect(true, WebuSocketCloseEnum.CLOSED_BY_TIMEOUT);
			}
			frame++;
		}
	}

	void OnApplicationQuit() {
		webSocket.Disconnect(true);
	}
}
