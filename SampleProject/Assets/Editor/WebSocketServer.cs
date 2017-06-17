using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Alchemy;

[InitializeOnLoad] public class WebSocketServer {
	static WebSocketServer () {
		Debug.LogError("start receiving");
		
		var s = new Alchemy.WebSocketServer(8080, IPAddress.Parse("127.0.0.1"));
		s.OnConnect += (c) => {
			Debug.LogError("fmm? c:" + c);
			// OnEventDelegate
		};

		s.Start();

		/*
			これで、WebSocketを受けたりするテストが書けるようになる。
			UnityTestで処理することができるかな〜どうだろ。
			
		 */
	}
}
