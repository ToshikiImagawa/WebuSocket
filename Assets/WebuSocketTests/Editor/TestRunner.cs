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
			// new Test_3_0_DisconnectFromServer(),
			new Test_3_1_DisconnectFromClient(),
			new Test_3_2_DisconnectWithClose(),
			new Test_3_3_DisconnectWithCloseSync(),
			// new Test_3_4_DisconnectWithoutClose(),//なんか斬新な切断方法思いついたらつける。
			
			// data correctness
			new Test_4_0_ReceiveManyTimes(),
			new Test_4_1_ReceiveManyManyTimes(),
			new Test_4_2_SendManyTimes(),
			new Test_4_3_SendManyManyTimes(),
			new Test_4_4_SendAndReceiveManyTimes(),
			new Test_4_5_SendAndReceiveManyManyTimes(),
			new Test_4_6_SendAndReceiveAsyncManyTimes(),
		};
		
		totalFrame = 0;
		
		Start();
		
		// ver 0.5.0
		// using new frequently.
		// 3586
		// 4162
		// 3632
		// 3861
		// 3677
		
		// total avg 3783.6
		
		// using Array.Resize. Winner!!
		// 3511
		// 3411
		// 3465
		// 3468
		// 3451
		
		// total avg 3461.2 FASTEST. ver 0.5.1
		
			// take2
			// 3452
			// 3610
			// 3397
			// 3400
			// 3455
			
			// total 3462.8
		
		
		// using pre-allocated buffer. 65535 * 10.
		// 3389
		// 3445
		// 3577
		// 3492
		// 3539
		
		// total avg 3488.4
		
			// take2
			// 3419
			// 3395
			// 3569
			// 3526
			// 3528
			
			// total avg 3487.4
		
		// ver 0.5.1 
		// 3416
		// 3574
		// 3447
		// 3504
		// 3502
		
		// total avg 3488.6
		
	}
	
	private int totalFrame;
	
	private void Start () {
		WebuSocketClient webuSocket = null;
		RunThrough(webuSocket, tests, Teardown);
	}
	
	private void Next () {
		tests.RemoveAt(0);
		
		if (!tests.Any()) {
			Debug.LogError("all tests finished. totalFrame:" + totalFrame);
			return;
		}
		
		Start();
	}

    private IEnumerator<int> Setup (WebuSocketClient webuSocket, ITestCase test) {
		Debug.LogWarning("test:" + test.ToString() + " started,");
		
		var optionalParams = test.OnOptionalSettings();
		var throttle = optionalParams.throttle;
		var hearderValues = optionalParams.headerValues;
		
		webuSocket = new WebuSocketClient(
			"ws://127.0.0.1:80/reflector_disque_client",
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
		
		var frame = 0;
		while (webuSocket.IsConnected()) {
			frame++;
			yield return 0;
		}
		
		while (webuSocket.State() != WebuSocketClient.WSConnectionState.Closed) {
			frame++;
			yield return 0;
		}
		
		// closed.
		Debug.Log("test:" + test.ToString() + " passed. spent frame:" + frame);
		yield return frame;
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
		
		var timeout = test.OnOptionalSettings().timeout;
		
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
					
					if (timeout < testFrameCount) {
						Debug.LogError("timeout:" + test.ToString());
						break;
					}
				}
				totalFrame += enumeration.Current;
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