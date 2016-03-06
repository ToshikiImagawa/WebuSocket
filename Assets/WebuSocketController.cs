using UnityEngine;
using System.Collections;
using WebuSocket;

public class WebuSocketController : MonoBehaviour {
	WebuSocketClient webuSocket;
	
	// Use this for initialization
	void Start () {
		webuSocket = new WebuSocketClient("ws", "127.0.0.1", 80);
		
		// new WebSocket(WEBSOCKET_ENTRYPOINT, customHeaderKeyValues, agent);

		// webSocket.OnOpen += (sender, e) => {
		// 	MainThreadDispatcher.Post(
		// 		() => {
		// 			connected();
		// 		}
		// 	);
		// };
		
		// Observable.EveryUpdate().Subscribe(
		// 	_ => {
		// 		if (0 < stringQueue.Count) {
		// 			List<string> messages;
		// 			lock (stringQueue) {
		// 				messages = new List<string>(stringQueue);
		// 				stringQueue.Clear();
		// 			}
		// 			onStringMessage(messages);
		// 		}
		// 	}
		// );
		// webSocket.OnMessage += (sender, e) => {
		// 	lock (stringQueue) stringQueue.Enqueue(e.Data);
		// };

		// webSocket.OnError += (sender, e) => {
		// 	MainThreadDispatcher.Post(
		// 		() => {
		// 			disconnected(e.Message);
		// 		}
		// 	);
			
		// 	CloseCurrentConnection();
			
		// 	if (autoReconnect) {
		// 		// auto reconnect after RECONNECTION_MILLISEC.
		// 		Observable.TimerFrame(RECONNECTION_MILLISEC).Subscribe(
		// 			_ => {
		// 				webSocket.Connect();
		// 				if (!webSocket.IsAlive) {
		// 					connectionFailed("could not re-connect to entry point:" + WEBSOCKET_ENTRYPOINT);
		// 				}
		// 			}
		// 		);
		// 	}
		// };

		// webSocket.OnClose += (sender, e) => {
		// 	Debug.Log("OnClose string version. なんもやってない。 e:" + e);
		// };
		
		// webSocket.ConnectAsync();
	}
	
	public void OnApplicationQuit () {
		Debug.LogError("close!");
		webuSocket.Close();
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
