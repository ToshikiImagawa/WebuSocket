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
		Debug.Log("server setup is over.");
	}
	
	private void Teardown () {
	}
	
	
	
	public void OnConnected (string connectionId, byte[] data) {
		// var playerIdString = Encoding.UTF8.GetString(data);
		Debug.Log("OnConnected connectionId:" + connectionId);
	}

	public void OnMessage (string connectionId, string data) {}
	
	public void OnMessage (string connectionId, byte[] data) {
		// Debug.Log("OnMessage connectionId:" + connectionId + " data:" + data.Length);
		
		var command = Encoding.UTF8.GetString(data);
		switch (command) {
			case "closeRequest": {
				PublishTo(data, connectionId);// no way to do this. because of luajit's bug.
				return;
			}
			case "10000DataRequest": {
				for (var i = 0; i < 10000; i++) PublishTo(data, connectionId);
				return;
			}
			case "100000DataRequest": {
				for (var i = 0; i < 100000; i++) PublishTo(data, connectionId);
				return;
			}
			case "10000DataRequestAsync": {
				var i = 0;
				ServerInitializer.SetupUpdaterThread(
					"10000DataRequestAsyncThread",
					() => {
						PublishTo(data, connectionId);
						i++;
						if (i == 10000) return false;
						return true;
					}
				);
				return;
			}
			case "100000DataRequestAsync": {
				var i = 0;
				ServerInitializer.SetupUpdaterThread(
					"100000DataRequestAsyncThread",
					() => {
						PublishTo(data, connectionId);
						i++;
						if (i == 100000) return false;
						return true;
					}
				);
				return;
			}
			case "1000DataSend": {
				return;
			}
			case "1000DataSendAndReturn": {
				break;
			}
			case "5000DataSend": {
				return;
			}
			case "10000DataSend": {
				return;
			}
			case "10000DataSendAndReturn": {
				break;
			}
			default: {
				// Debug.LogError("server received unknown command:" + command);
				break;
			}
		}
		 
		// reflect.
		PublishTo(data, connectionId);
	}

	public void OnDisconnected (string connectionId, byte[] data, string reason) {
		Debug.Log("OnDisconnected connectionId:" + connectionId + " reason:" + reason);
	}
	
	/**
		publisher methods
	*/
	private Action<object, string> PublishTo = NotYetReady;
	public void SetPublisher (Action<object, string> publisher) {
		PublishTo = publisher;
		Setup();
	}

	private static void NotYetReady (object obj, string connectionId) {
		Debug.Log("not yet publishable.");
	}
}