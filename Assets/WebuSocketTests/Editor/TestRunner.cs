using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

using WebuSocket;

public class TestRunner {
	[MenuItem("WebuSocketTest/RunTests")] public static void RunTests () {
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
		var done = false;
		
		webuSocket = new WebuSocketClient(
			"ws://127.0.0.1:80/calivers_disque_client",
			() => {
				test.OnConnect(webuSocket);
			},
			datas => {
				test.OnReceived(webuSocket, datas);
				done = true;
			},
			closeReason => {
				Debug.LogWarning("closeReason:" + closeReason);
			},
			errorReason => {
				Debug.LogError("errorReason:" + errorReason);
			}
		);
		
		while (!done) {
			yield return null;
		}
		
		webuSocket.Close();
		
		while (webuSocket.State() != WebuSocketClient.WSConnectionState.Closed) {
			yield return null;
		}
		
		// closed.
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
		Debug.LogError("test:" + test.ToString() + " started,");
		
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