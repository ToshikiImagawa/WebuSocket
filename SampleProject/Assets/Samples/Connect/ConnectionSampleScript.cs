using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

using WebuSocketCore;


/**
	webuSocket connection sample.
*/
public class ConnectionSampleScript : MonoBehaviour {
	
	WebuSocket webSocket;
	
	bool opened = false;

	public const string userId = "test";
	UdpClient udp;
	
	private void ThreadMethod () {
        while(true)
        {
            IPEndPoint remoteEP = null;
            byte[] data = udp.Receive(ref remoteEP);
			using (var sw = new System.IO.StreamWriter("received", true)) {
				sw.WriteLine(data.Length);
			}
            string text = Encoding.ASCII.GetString(data);
            Debug.LogError(text);
        }
    } 
	public static string GetLocalIPAddress () {
		var host = Dns.GetHostEntry(Dns.GetHostName());
		foreach (var ip in host.AddressList) {
			if (ip.AddressFamily == AddressFamily.InterNetwork) {
				return ip.ToString();
			}
			Debug.Log("ip.ToString():" + ip.ToString());
		}
		throw new Exception("No network adapters with an IPv4 address in the system!");
	}

	void Start () {
		IPAddress localIP;
		using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
			socket.Connect("8.8.8.8", 65530);
			var endPoint = socket.LocalEndPoint as IPEndPoint;
			localIP = endPoint.Address;
			Debug.Log("localIP:" + localIP.ToString());
		}

		// クライアント側は自分が接続しているエンドポイントのipがわかればそれでいい。そこにudpが降ってくるのを待つ。
		if (true) {
			udp = new UdpClient(new IPEndPoint(new IPAddress(localIP.GetAddressBytes()), 7777));
			udp.Client.ReceiveTimeout = 1000;

			var thread = new Thread(new ThreadStart(ThreadMethod));
			thread.Start();
		}

		webSocket = new WebuSocket(
			// url.
			// "wss://echo.websocket.org:443/",
			"ws://127.0.0.1:8080/sample_disque_client",

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
				{"debugport", localIP.ToString()}
			}
		);
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
