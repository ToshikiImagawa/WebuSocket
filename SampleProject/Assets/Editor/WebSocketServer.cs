using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// using Alchemy;

[InitializeOnLoad] public class WebSocketServer {
	// サーバ起動を行う。

	static WebSocketServer () {
		Debug.LogError("ghere!");
		// var s = new Alchemy.WebSocketServer();
		// var d = s.Clients;
		
		// var dllPath = "Library/ScriptAssemblies/Assembly-CSharp.dll";//Assembly-CSharp-Editor.dll
		;

		var q = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var a in q) {
			Debug.LogError("a:" + a);
		}
		
		// aServer.Start();
	}
}
