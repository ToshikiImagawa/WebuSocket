using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using UnityEditor;
using UnityEngine;

using WebuSocket;

public class TestRunner {
	[MenuItem("WebuSocketTest/RunTests")] public static void RunTests () {
		/*
			note that this tests requires echo WebSocket server.
			And Server code is not contained this repo yet.
		*/
		var testRunner = new TestRunner();
	}
	
	List<ITestCase> tests;
	public TestRunner () {
		tests = new List<ITestCase> {
			new Test_0_0_OrderAndDataShouldMatch(),
			new Test_0_1_SizeMatching_126(),
			new Test_0_2_SizeMatching_127(),
			new Test_0_3_SizeMatching_65534(),
			new Test_0_4_SizeMatching_65535(),
			new Test_0_5_SizeMatching_14140(),
			
			// with throttle
			new Test_1_0_OrderAndDataShouldMatchWithThrottle(),
			new Test_1_1_SizeMatching_126WithThrottle(),
			new Test_1_2_SizeMatching_127WithThrottle(),
			new Test_1_3_SizeMatching_65534WithThrottle(),
			new Test_1_4_SizeMatching_65535WithThrottle(),
			new Test_1_5_SizeMatching_14140WithThrottle(),
			
			// multiple receive
			new Test_2_0_TwoPingReceivedOnSameFrame(),
			new Test_2_1_TwoSmall125MessageReceivedOnSameFrame(),
			new Test_2_2_Two125_126MessageReceivedOnSameFrame(),
			new Test_2_3_Two127_127MessageReceivedOnSameFrame(),
			new Test_2_4_Two128_128MessageReceivedOnSameFrame(),
			new Test_2_5_LargeSizeMessageReceived(),
			
			// detect disconnection
			
		};
		
		Start();
	}
	
	private void Start () {
		WebuSocketClient webuSocket = null;
		RunThrough(webuSocket, tests, Teardown);
	}
	
	private void Next () {
		tests.RemoveAt(0);
		
		if (!tests.Any()) {
			Debug.LogError("all tests finished.");
			return;
		}
		
		Start();
	}

    private IEnumerator Setup (WebuSocketClient webuSocket, ITestCase test) {
		Debug.LogWarning("test:" + test.ToString() + " started,");
		
		var optionalParams = test.OnOptionalSettings();
		var throttle = optionalParams.throttle;
		var hearderValues = optionalParams.headerValues;
		
		webuSocket = new WebuSocketClient(
			"ws://127.0.0.1:80/calivers_disque_client",
			() => {
				test.OnConnect(webuSocket);
			},
			datas => {
				test.OnReceived(webuSocket, datas);
			},
			closeReason => {
				Debug.LogWarning("closeReason:" + closeReason);
			},
			(errorReason, exception) => {
				Debug.LogError("errorReason:" + errorReason);
				if (exception != null) {
					if (exception.GetType() == typeof(SocketException)) Debug.LogError("SocketErrorCode:" + ((SocketException)exception).SocketErrorCode);
				}
			}
		);
		
		while (webuSocket.IsConnected()) {
			yield return null;
		}
		
		while (webuSocket.State() != WebuSocketClient.WSConnectionState.Closed) {
			yield return null;
		}
		
		// closed.
		Debug.LogWarning("test:" + test.ToString() + " overed.");
    }
	
	private void Teardown (WebuSocketClient webuSocket) {
		if (webuSocket != null) {
			Debug.LogError("failed to close.");
			webuSocket.CloseSync();
		}
		webuSocket = null;
	}
	
	
	private Thread RunThrough (WebuSocketClient webuSocket, List<ITestCase> tests, Action<WebuSocketClient> Teardown) {
		var framePerSecond = 60;
		var mainThreadInterval = 1000f / framePerSecond;
		
		var test = tests[0];
		Action loopMethod = () => {
			try {
				var enumeration = Setup(webuSocket, test);
				
				double nextFrame = (double)System.Environment.TickCount;
				
				var before = 0.0;
				var tickCount = (double)System.Environment.TickCount;
				
				var testFrameCount = 0;
				
				while (true) {
					tickCount = System.Environment.TickCount * 1.0;
					if (nextFrame - tickCount > 1) {
						Thread.Sleep((int)(nextFrame - tickCount)/2);
						/*
							waitを半分くらいにすると特定フレームで安定する。よくない。
						*/
						continue;
					}
					
					if (tickCount >= nextFrame + mainThreadInterval) {
						nextFrame += mainThreadInterval;
						continue;
					}
					
					// run action for update.
					var continuation = enumeration.MoveNext();
					if (!continuation) break;
					
					nextFrame += mainThreadInterval;
					before = tickCount;
					
					testFrameCount++;
					
					// wait 5sec for timeout. 
					if (60 * 5 < testFrameCount) {
						Debug.LogError("timeout:" + test.ToString());
						break;
					}
				}
				
				Teardown(webuSocket);
			} catch (Exception e) {
				Debug.LogError("test loopId:" + test.ToString() + " error:" + e);
			}
			
			Next();
		};
		
		var thread = new Thread(new ThreadStart(loopMethod));
		thread.Start();
		return thread;
	}

}