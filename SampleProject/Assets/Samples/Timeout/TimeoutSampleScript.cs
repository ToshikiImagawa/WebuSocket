using System.Collections;

using UnityEngine;

using WebuSocketCore;


/**
	sample of timeout. 
*/
public class TimeoutSampleScript : MonoBehaviour {

	WebuSocket webSocket;
	bool opened = false;

	void Start () {
		webSocket = new WebuSocket(
			"wss://echo.websocket.org:443/",
			1024,
			() => {
				Debug.Log("connect succeeded.");
				opened = true;
			},
			datas => {},
			() => {},
			closeReason => {
				Debug.Log("closed, closeReason:" + closeReason);
			},
			(error, ex) => {
				Debug.LogError("error:" + error + " ex:" + ex);
			}
		);
	}

	void Update () {
		/*
			IsConnected(newTimeoutSec) method can detect timeout of websocket connection.
			You can call this method at your appropriate intervals for connectivity checking.

			default timeout sec is 10.
		*/
		if (opened && !webSocket.IsConnected(5)) {
			Debug.LogError("timeout detected.");
			opened = false;
		} 
	}

	void OnApplicationQuit() {
		webSocket.Disconnect(true);
	}
}
