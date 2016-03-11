using UnityEngine;
using WebuSocket;
using System.Collections.Generic;
using System;
using System.Net.Sockets;

public class WebuSocketController : MonoBehaviour {
	WebuSocketClient webuSocket;
	
	string playerId = "thePlayerIdOfWebuSocket";
	
	private Queue<byte[]> byteQueue = new Queue<byte[]>();
	
	// Use this for initialization
	void Start () {
		var serverURL = "ws://127.0.0.1:80/reflector_disque_client";
		
		webuSocket = new WebuSocketClient(
			serverURL,
			() => {
				Debug.Log("connected to server:" + serverURL);
			},
			(Queue<byte[]> datas) => {
				lock (byteQueue) {
					while (0 < datas.Count) byteQueue.Enqueue(datas.Dequeue());
				}
			},
			(string closedReason) => {
				Debug.LogError("connection closed by reason:" + closedReason);
			},
			(string errorMessage, Exception e) => {
				Debug.LogError("connection error:" + errorMessage);
				if (e != null) {
					if (e.GetType() == typeof(SocketException)) Debug.LogError("SocketErrorCode:" + ((SocketException)e).SocketErrorCode);
				}
			},
			1,// 1/1FPS
			new Dictionary<string, string>{
				{"User-Agent", "testAgent"},
				{"playerId", playerId}
			}
		);
		
		Debug.LogError("webuSocket:" + webuSocket.webSocketConnectionId);
	}
	
	private void OnMessageReceived (Queue<byte[]> messages) {
		Debug.Log("OnMessageReceived messages:" + messages.Count);
	}
	
	public void OnApplicationQuit () {
		webuSocket.CloseSync();
	}
	
	int frame = 0;
	
	// Update is called once per frame
	void Update () {
		if (webuSocket != null && webuSocket.IsConnected()) {
			if (frame == 50) {
				// ping on frame.
				webuSocket.Ping(()=>Debug.Log("pong"));
				
				webuSocket.Send(new byte[]{0x01});
			}
			
			if (frame == 300) {
				webuSocket.Close();
			}
			
			frame++;
		}
		
		/*
			receive block of message.
		*/
		if (0 < byteQueue.Count) {
			lock (byteQueue) {
				OnMessageReceived(byteQueue);
				byteQueue.Clear();
			}
		}
	}
}
