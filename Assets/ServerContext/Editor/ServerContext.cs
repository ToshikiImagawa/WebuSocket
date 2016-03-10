using System;
using System.Linq;
using System.Collections.Generic;

using System.IO;
using System.Text;
using UnityEngine;

public class ServerContext {
	
	private readonly string serverContextId;

	
	public ServerContext () {
		serverContextId = Guid.NewGuid().ToString();
	}
	
	private void Setup () {
	}
	
	/**
		ServerContextの終了手続き
	*/
	private void Teardown () {
	}
	
	
	
	public void OnConnected (string connectionId, byte[] data) {
		var playerIdString = Encoding.UTF8.GetString(data);
		Debug.LogError("OnConnected! playerIdString:" + playerIdString);
	}

	public void OnMessage (string connectionId, string data) {}
	
	public void OnMessage (string connectionId, byte[] data) {
		Debug.LogError("データ届いた data:" + data.Length);
	}

	public void OnDisconnected (string connectionId, byte[] data, string reason) {}
	
	/**
		publisher methods
	*/
	private Action<object, string> PublishTo = NotYetReady;
	public void SetPublisher (Action<object, string> publisher) {
		PublishTo = publisher;
		Setup();
	}

	private static void NotYetReady (object obj, string connectionId) {
		// XrossPeer.Log("not yet publishable.");
	}
}