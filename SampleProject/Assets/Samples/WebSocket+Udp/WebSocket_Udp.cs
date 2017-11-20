using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AutoyaFramework.Connections.Udp;
using UnityEngine;
using WebuSocketCore;

public class WebSocket_Udp : MonoBehaviour {

	UdpReceiver udpUnit;
	int udpReceivePort = 8080;

	private string serverIP = "13.230.48.184";
	private WebuSocket webuSocket;

	// Use this for initialization
	void Start () {
		// udpサーバを立ち上げ、往復 + wsの接続を行う。
		// タイムアウトまでに帰ってこなければ、wsのみの接続を行う。
		using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
			socket.Connect("8.8.8.8", 65530);
			var endPoint = socket.LocalEndPoint as IPEndPoint;
			var localIP = endPoint.Address;

			var first = true;
			udpUnit = new UdpReceiver(
				localIP, 
				udpReceivePort, 
				udpData => {
					var param = Encoding.UTF8.GetString(udpData);

					if (first) {
						first = false;
						ConnectWebSocket("testUser", param.Split(':')[1]);
					} else {
						// udp data received.
						Debug.Log("param:" + param);
					}
				},
				new IPEndPoint(IPAddress.Parse(serverIP), udpReceivePort)
			);

			udpUnit.Send(new byte[]{1});

			Debug.Log("localIP:" + localIP.ToString());
		}
	}

	private void ConnectWebSocket (string userId, string udpPort) {
		webuSocket = new WebuSocket(
			// url.
			"ws://" + serverIP + ":"+ udpReceivePort + "/sample_disque_client",

			// buffer size.
			1024,
			() => {
				Debug.Log("connected to websocket echo-server. send hello to echo-server");
			},
			datas => {
				while (0 < datas.Count) {
					ArraySegment<byte> data = datas.Dequeue();

					byte[] bytes = new byte[data.Count];
					Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

					Debug.Log("message:" + Encoding.UTF8.GetString(bytes));
				}
			},
			() => {
				Debug.Log("received server ping. automatically ponged.");
			},
			closeReason => {
				Debug.Log("closed, closeReason:" + closeReason);
			},
			(errorEnum, exception) => {
				Debug.LogError("error, errorEnum:" + errorEnum + " exception:" + exception);
			},
			new Dictionary<string, string>{
				// // set WebSocket connection header parameters here!
				{"id", userId},
				// // // {"debugaddr", udpIp},
				{"debugport", udpPort}
			}
		);
	}
	
	// Update is called once per frame
	void OnApplicationQuit () {
		if (webuSocket != null) {
			webuSocket.Disconnect();
		}
		if (udpUnit != null) {
			udpUnit.Close();
		}
	}
}
