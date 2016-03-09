using System;
using System.Collections.Generic;
using UnityEngine;
using WebuSocket;

/*
	tests for 1 frame per sec.
*/

public class TestFrame2 {
	public const int throttleFrame = 1;
}

public class Test_2_0_TwoPingReceivedOnSameFrame : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(TestFrame2.throttleFrame);
    }
	
	private int count = 0;
	
    public void OnConnect(WebuSocketClient webuSocket) {
		Action act = () => {
			count++;
			if (count == 2) {
				webuSocket.Close();
			}
		};
		
        webuSocket.Ping(act);
		webuSocket.Ping(act);
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {}
}