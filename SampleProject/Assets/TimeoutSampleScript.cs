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
				Debug.Log("connection succeeded.");
                opened = true;
			},
			datas => {},
			() => { },
			closeReason => {
				Debug.Log("closed, closeReason:" + closeReason);
			},

            /*
                timeout parameter.
                -1 is not good. only for sample.

                I think you may set large number than 0.
            */
            timeoutSec:-1
		);
	}

    void Update () {
        /*
            IsConnected() method can detect timeout of connection.
            You can call this method by calling it at appropriate intervals.
        */
        if (opened && !webSocket.IsConnected()) {
            Debug.LogError("timeout detected.");
            opened = false;
        } 
    }

	void OnApplicationQuit() {
		webSocket.Disconnect(true);
	}
}
