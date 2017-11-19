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
	// private string serverIP = "127.0.0.1";
	private int portNum = 8080;





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
					Debug.Log("サーバからudpでのレスポンスは来た");
					var ipAndPort = text.Split(':');
					var currentReceivedIp = ipAndPort[0];// サーバが返してきたクライアントのglobal ip
					var currentReceivedPort = ipAndPort[1];// サーバが返してきたクライアントのglobal port
					
					Connect(currentReceivedIp, currentReceivedPort);
					continue;
				}

				Debug.Log("udp received:" + text);
				Action act2 = () => {
					achieved2.text += "true. text:" + text;
				};
				Enqueue(act2);
				achieved = true;
			} catch (Exception e) {
				Debug.LogError("e:" + e);
				Thread.CurrentThread.Abort();
			}
        }
    }

	IPAddress localIP;


	void Start () {
		
		// udpClientでデータを送るために、自分のglobal ipを得る
		using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
			socket.Connect("8.8.8.8", 65530);
			var endPoint = socket.LocalEndPoint as IPEndPoint;
			localIP = endPoint.Address;
			Debug.Log("localIP:" + localIP.ToString());
		}

		// udpClientを作成し、epを指定してサーバへとudpを送付する。
		// これで上りの経路がNATに記録される。
		if (true) {
			udp = new UdpClient(new IPEndPoint(new IPAddress(localIP.GetAddressBytes()), portNum));
			var ep = new IPEndPoint(IPAddress.Parse(serverIP), portNum);
			
			// 送るデータはなんでもいい。
			var bytes = Encoding.UTF8.GetBytes("hello!");
			
			udp.Send(bytes, bytes.Length, ep);
			var thread = new Thread(new ThreadStart(ThreadMethod));
			thread.Start();
		}
	}

	private void Connect (string udpIp, string udpPort) {
		webSocket = new WebuSocket(
			// url.
			"ws://" + serverIP + ":"+ portNum + "/sample_disque_client",

			// buffer size.
			1024,

			// handler for connection established to server.
			() => {
				opened = true;
				Debug.Log("connected to websocket echo-server. send hello to echo-server");
				// webSocket.SendString("hello!");
				// webSocket.SendString("wooooo!");
				// webSocket.SendString("looks!");
				// webSocket.SendString("fine!");
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

					// もし数字がかえっていたら、udpで通信してみる。
					try {
						var portNum = Convert.ToUInt16(Encoding.UTF8.GetString(bytes));

						udp = new UdpClient(new IPEndPoint(new IPAddress(localIP.GetAddressBytes()), portNum));
						var ep = new IPEndPoint(IPAddress.Parse(serverIP), portNum);
						var bytes2 = Encoding.UTF8.GetBytes("hello! again.");
						
						udp.Send(bytes2, bytes2.Length, ep);
						Debug.Log("udp sended. target portNum:" + portNum);

						// var bytes3 = Encoding.UTF8.GetBytes(portNum.ToString());
						// webSocket.Send(bytes3);
					} catch {}
					
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
				// // {"debugaddr", udpIp},
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
