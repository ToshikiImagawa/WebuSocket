using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using WebuSocketCore;


/**
	webuSocket connection sample.
*/
public class ConnectionSampleScript : MonoBehaviour {
	public Text times;
	public Text achieved2;
	
	WebuSocket webSocket;
	private string serverIP = "13.230.48.184";

	bool opened = false;

	public const string userId = "test";
	UdpClient udp;
	IPEndPoint remoteEP = null;

	private int udpReceiveCount;
	private bool achieved;

	void OnGUI () {
		GUILayout.Label("udpReceiveCount:" + udpReceiveCount);
		GUILayout.Label("achieved:" + achieved);
	}

	private List<Action> acts = new List<Action>();
	private object lockObj = new object();
	private void Enqueue (Action act) {
		lock (lockObj) {
			acts.Add(act);
		}
	}

	private void ThreadMethod () {


        while(true)
        {
			try {
				Debug.Log("start waiting.");
				byte[] data = udp.Receive(ref remoteEP);
				remoteEP = null;
				string text = Encoding.ASCII.GetString(data);
				udpReceiveCount++;
				Action act = () => {
					times.text += "+1 ";
				};
				Enqueue(act);

				if (text.Contains(":")) {
					// ここまでの受信はできる。

					var ipAndPort = text.Split(':');
					var currentReceivedIp = ipAndPort[0];// サーバが返してきたクライアントのglobal ip
					var currentReceivedPort = ipAndPort[1];// サーバが返してきたクライアントのglobal port
					
					
					// var currentReceivedIp = ((IPEndPoint)(udp.Client.LocalEndPoint)).Address.ToString();// このipはローカルipなのでグローバルではない、LAN内でのみ使える。(たまたま一致する。)
					// var currentReceivedPort = port.ToString();// 接続時に使ったポート。
					Connect(currentReceivedIp, currentReceivedPort);
					continue;
				}

				Action act2 = () => {
					achieved2.text += "true";
				};
				Enqueue(act2);
				achieved = true;
			} catch (Exception e) {
				Debug.LogError("e:" + e);
				Thread.CurrentThread.Abort();
			}
        }
    }
	private int port;

	void Start () {
		IPAddress localIP;
		using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
			socket.Connect("8.8.8.8", 65530);
			var endPoint = socket.LocalEndPoint as IPEndPoint;
			localIP = endPoint.Address;
			Debug.Log("localIP:" + localIP.ToString());
		}

		
		// // サーバへとudpを送付する
		// if (false) {
		// 	using (var udpSender = new UdpClient()) {
		// 		// udpSender.Connect(IPAddress.Parse("127.0.0.1"), 7777);
		// 		// public int Send(byte[] dgram, int bytes, IPEndPoint endPoint);
		// 		var ep = new IPEndPoint(IPAddress.Parse(serverIP), 7777);
		// 		// var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7777);
		// 		udpSender.Send(new byte[]{1,2,3,4}, 4, ep);
		// 		port = ((IPEndPoint)udpSender.Client.LocalEndPoint).Port;
		// 	}
		// 	Debug.Log("send udp. port:" + port);
		// }

		// クライアント側は自分が接続しているエンドポイントのipがわかればそれでいい。そこにudpが降ってくるのを待つ。
		// 送信と受信を一手にできそう。、、もしかしてこれがいけないのか？
		if (true) {
			udp = new UdpClient(new IPEndPoint(new IPAddress(localIP.GetAddressBytes()), 8080));
			var ep = new IPEndPoint(IPAddress.Parse(serverIP), 8080);
			var bytes = Encoding.UTF8.GetBytes("hello!");
			
			udp.Send(bytes, bytes.Length, ep);
			
			port = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
			Debug.Log("udp sending port:" + port);
			// udp.Client.ReceiveTimeout = 1000;

			var thread = new Thread(new ThreadStart(ThreadMethod));
			thread.Start();
		}
	}

	private void Connect (string udpIp, string udpPort) {
		Debug.Log("udp receiving udpIp:" + udpIp + " port:" + udpPort);
		// var udp2 = new UdpClient();
		// var ep = new IPEndPoint(IPAddress.Parse(serverIP), Convert.ToInt32(udpPort));
		// udp2.Send(new byte[]{1,2,3,4}, 4, ep);
			
		webSocket = new WebuSocket(
			// url.
			// "wss://echo.websocket.org:443/",
			// "ws://127.0.0.1:8080/sample_disque_client",
			"ws://" + serverIP + ":8080/sample_disque_client",

			// buffer size.
			1024,

			// handler for connection established to server.
			() => {
				opened = true;
				Debug.Log("connected to websocket echo-server. send hello to echo-server");
				webSocket.SendString("hello!");
				webSocket.SendString("wooooo!");
				webSocket.SendString("looks!");
				webSocket.SendString("fine!");
			},

			// handler for receiving data from server. 
			datas => {
				/*
					this handler is called from system thread. not Unity's main thread.
					
					and, datas is ArraySegment<byte> x N. 

					SHOULD COPY byte data from datas HERE.

					do not copy ArraySegment<byte> itself.
					these data array will be destroyed soon after leaving this block.
				*/
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
				// set WebSocket connection header parameters here!
				{"id", userId},
				// {"debugaddr", udpIp},
				{"debugport", udpPort}
			}
		);
	}
	void Update () {
		lock (lockObj) {
			if (acts.Any()) {
				acts.ForEach(a => a());
				acts.Clear();
			}
		}
	}
	void OnApplicationQuit () {
		if (webSocket != null && webSocket.IsConnected()) {
			webSocket.Disconnect();
		}
		if (udp != null) {
			udp.Close();
		}
	}
}
