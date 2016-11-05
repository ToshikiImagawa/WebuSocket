using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using WebuSocketCore;


/**
	simple websocket connection class.
*/
public class SampleScript : MonoBehaviour {
	// 
	WebuSocket webSocket;

	void Start () {

		webSocket = new WebuSocket(
			// url.
			"wss://echo.websocket.org:443/",

			// buffer size.
			1024,

			// handler for connection established to server.
			() => {
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
			}

			// other handlers are for error and close event handling.
		);
	}
}
