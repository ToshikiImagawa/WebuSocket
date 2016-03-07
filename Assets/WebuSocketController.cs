using UnityEngine;
using System.Collections;
using WebuSocket;
using System.Collections.Generic;

public class WebuSocketController : MonoBehaviour {
	WebuSocketClient webuSocket;
	
	string playerId = "thePlayerIdOfWebuSocket";
	
	private Queue<byte[]> byteQueue = new Queue<byte[]>();
	
	// Use this for initialization
	void Start () {
				
		// Observable.EveryUpdate().Subscribe(
		// 	_ => {
		// 		lock (byteQueue) {
		// 			List<byte[]> messages;
		// 			if (0 < byteQueue.Count) {
		// 				messages = new List<byte[]>(byteQueue);
		// 				byteQueue.Clear();
		// 			}
		// 			onByteMessage(messages);
		// 		}
		// 	}
		// );
		
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
				lock (byteQueue) {
					while (0 < datas.Count) byteQueue.Enqueue(datas.Dequeue());
				}
			},
			(string closedReason) => {
				Debug.LogError("connection closed by reason:" + closedReason);
			},
			(string error) => {
				Debug.LogError("connection error:" + error);
			},
			new Dictionary<string, string>{
				{"User-Agent", "testAgent"},
				{"playerId", playerId}
			}
		);
		
		Debug.LogError("webuSocket:" + webuSocket.webSocketConnectionId);
	}
	
	public void OnApplicationQuit () {
		webuSocket.CloseSync();
	}
	
	int frame = 0;
	
	// Update is called once per frame
	void Update () {
		if (webuSocket.IsConnected()) {
			// if (frame == 50) {
			// 	Debug.LogError("start 50");
			// 	// ping on frame.
			// 	webuSocket.Ping();
			// 	webuSocket.Ping();
				
			// 	// 1000 message per frame without stall.
			// 	for (var i = 0; i < 1000; i++) webuSocket.Send(new byte[]{1,2,3,4});
				
			// 	// 126 bytes.
			// 	var data126 = new byte[126];
			// 	for (var i = 0; i < data126.Length; i++) data126[i] = 1;
			// 	webuSocket.Send(data126);
			// }
			
			if (frame == 100) {
				Debug.LogError("start 100");
				var data65535 = new byte[65535];
				for (var i = 0; i < data65535.Length; i++) data65535[i] = 1;
				webuSocket.Send(data65535);
			}
			// サーバ側のライブラリの受け取り最大値は65535らしい。
			
			frame++;
		}
		
		if (0 < byteQueue.Count) {
			lock (byteQueue) {
				Debug.LogError("queued data count:" + byteQueue.Count);
				foreach (var data in byteQueue) {
					Debug.LogError("data:" + data.Length);
				}
				byteQueue.Clear();
			}
		}
	}
}
