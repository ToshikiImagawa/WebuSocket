using System;
using System.Collections.Generic;
using Miyamasu;
using UnityEngine;
using WebuSocketCore;

public class Receiver {
	public int connectedCount = 0;
	public List<byte[]> byteDatas = new List<byte[]>();
	public int pingedCount = 0;
	public string closedReason;
	public string errorReason;
	public Exception errorException;

	public void Connected () {
		connectedCount++;
	}
	public void Received (Queue<ArraySegment<byte>> incomingDatas) {
		while (0 < incomingDatas.Count) {
			var incomingArraySegment = incomingDatas.Dequeue();
			
			var byteData = new byte[incomingArraySegment.Count];
			Buffer.BlockCopy(incomingArraySegment.Array, incomingArraySegment.Offset, byteData, 0, incomingArraySegment.Count);
			byteDatas.Add(byteData);
		}
	}
	public void Pinged () {
		pingedCount++;
	}
	public void Closed (string reason) {
		closedReason = reason;
	}
	public void Error (string error, Exception ex) {
		errorReason = error;
		errorException = ex;
	}
}


public class Test0 : MiyamasuTestRunner {
	WebuSocket webuSocket;

	private Receiver receiverInstance;

	[MSetup] public void Setup () {
		receiverInstance = new Receiver();
		
		var connected = false;
		webuSocket = new WebuSocket(
			"ws://127.0.0.1:8081",
			1024 * 100,
			() => {
				connected = true;
				receiverInstance.Connected();
			},
			arraySegments => {
				receiverInstance.Received(arraySegments);
			},
			() => {
				receiverInstance.Pinged();
			},
			closedReason => {
				receiverInstance.Closed(closedReason.ToString());
			},
			(errorReason, ex) => {
				receiverInstance.Error(errorReason.ToString(), ex);
			},
			new Dictionary<string, string>()
		);
		WaitUntil(() => connected, 3);
		Assert(webuSocket.IsConnected(), "not connected.");
	}
	

	[MTeardown] public void Teardown () {
		webuSocket.Disconnect(true); 
	}

	[MTest] public void SendBytesAndReceive () {
		webuSocket.Send(new byte[]{1,2,3,4});

		WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

		Assert(receiverInstance.byteDatas[0].Length == 4, "not match.");
		
		Assert(receiverInstance.byteDatas[0][0] == 1, "not match.");
		Assert(receiverInstance.byteDatas[0][1] == 2, "not match.");
		Assert(receiverInstance.byteDatas[0][2] == 3, "not match.");
		Assert(receiverInstance.byteDatas[0][3] == 4, "not match.");
	}

	[MTest] public void SizeMatch126 () {
		var data = new byte[126];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);

		WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

		Assert(receiverInstance.byteDatas[0].Length == 126, "not match.");
		for (var i = 0; i < receiverInstance.byteDatas[0].Length; i++) Assert(receiverInstance.byteDatas[0][i] == 1, "not match. actual:" + receiverInstance.byteDatas[0][i] + " at:" + i);
	}
	
	[MTest] public void SizeMatch127 () {
		var data = new byte[127];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);

		WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

		Assert(receiverInstance.byteDatas[0].Length == 127, "not match.");
		for (var i = 0; i < receiverInstance.byteDatas[0].Length; i++) Assert(receiverInstance.byteDatas[0][i] == 1, "not match. actual:" + receiverInstance.byteDatas[0][i] + " at:" + i);
	}

	[MTest] public void SizeMatch3000 () {
		var data = new byte[3000];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);
		
		WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

		Assert(receiverInstance.byteDatas[0].Length == 3000, "not match.");
		for (var i = 0; i < receiverInstance.byteDatas[0].Length; i++) Assert(receiverInstance.byteDatas[0][i] == 1, "not match. actual:" + receiverInstance.byteDatas[0][i] + " at:" + i);
	}

	[MTest] public void SizeMatch65534 () {
		var data = new byte[65534];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);

		WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

		Assert(receiverInstance.byteDatas[0].Length == 65534, "not match.");
		for (var i = 0; i < receiverInstance.byteDatas[0].Length; i++) Assert(receiverInstance.byteDatas[0][i] == 1, "not match. actual:" + receiverInstance.byteDatas[0][i] + " at:" + i);
	}
	
	// [MTest] public void SizeMatch65535 () {
	// 	var data = new byte[65535];
	// 	for (var i = 0; i < data.Length; i++) data[i] = 1;
	// 	webuSocket.Send(data);

	// 	WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

	// 	Assert(receiverInstance.byteDatas[0].Length == 65535, "not match.");
	// 	for (var i = 0; i < receiverInstance.byteDatas[0].Length; i++) Assert(receiverInstance.byteDatas[0][i] == 1, "not match. actual:" + receiverInstance.byteDatas[0][i] + " at:" + i);
	// }

	// [MTest] public void SizeMatch65536 () {
	// 	var data = new byte[65536];
	// 	for (var i = 0; i < data.Length; i++) data[i] = 1;
	// 	webuSocket.Send(data);

	// 	WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

	// 	Assert(receiverInstance.byteDatas[0].Length == 65536, "not match.");
	// }
	
	// [MTest] public void SizeMatch141400 () {
	// 	var data = new byte[141400];
	// 	for (var i = 0; i < data.Length; i++) data[i] = 1;
	// 	webuSocket.Send(data);

	// 	WaitUntil(() => 0 < receiverInstance.byteDatas.Count, 3);

	// 	Assert(receiverInstance.byteDatas[0].Length == 141400, "not match.");
	// }
	
}



public interface ITestCase {
	OptionalSettings OnOptionalSettings ();
	void OnConnect (WebuSocket webuSocket);
	void OnReceived (WebuSocket webuSocket, Queue<byte[]> datas);
}

public struct OptionalSettings {
	public int throttle;
	public Dictionary<string, string> headerValues;
	public int timeout;
	
	public OptionalSettings (int throttle=0, Dictionary<string, string> headerValues=null, int timeout=60 * 5) {
		this.throttle = throttle;
		this.headerValues = headerValues;
		this.timeout = timeout;
	}
}

public static class DefaultSetting {
	public static OptionalSettings Default () {
		return new OptionalSettings(0, null, 60*5);
	}
}