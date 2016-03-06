using UnityEngine;
using System.Collections;
using WebuSocket;
using System.Collections.Generic;

public class WebuSocketController : MonoBehaviour {
	WebuSocketClient webuSocket;
	
	// Use this for initialization
	void Start () {
		webuSocket = new WebuSocketClient(
			"ws://127.0.0.1:80/calivers_disque_client",
			() => {
				Debug.LogError("connected!");
				// MainThreadDispatcher.Post(
				// 	() => {
				// 		Debug.LogError("connected.");
				// 	}
				// );
			},
			(Queue<byte[]> datas) => {
				Debug.LogError("data received!");
			},
			(string closedReason) => {
				Debug.LogError("connection closed by reason:" + closedReason);
			},
			(string error) => {
				Debug.LogError("connection error:" + error);
			}
		);
		Debug.LogError("webuSocket:" + webuSocket.webSocketConnectionId);
	}
	
	public void OnApplicationQuit () {
		Debug.LogError("close!");
		webuSocket.CloseSync();
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
