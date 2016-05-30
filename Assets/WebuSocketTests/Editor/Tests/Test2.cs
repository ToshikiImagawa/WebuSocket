using System;
using System.Collections.Generic;
using UnityEngine;
using WebuSocketCore;

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
	
    public void OnConnect(WebuSocket webuSocket) {
		Action act = () => {
			count++;
			if (count == 2) {
				webuSocket.Disconnect();
			}
		};
		
        webuSocket.Ping(act);
		webuSocket.Ping(act);
    }
	
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {}
}


public class Test_2_1_TwoSmall125MessageReceivedOnSameFrame : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(TestFrame2.throttleFrame);
    }
	
    public void OnConnect(WebuSocket webuSocket) {
		var message = new byte[125];
		for (var i = 0; i < message.Length; i++) message[i] = (byte)i;
		webuSocket.Send(message);
		webuSocket.Send(message);
    }
	
	private int count;
	
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {
		while (0 < datas.Count) {
			var data0 = datas.Dequeue();
			
			if (data0.Length != 125) Debug.LogError("faild to match Test_2_1_TwoSmall125MessageReceivedOnSameFrame. data0.Length:" + data0.Length);
			
			count++;
			if (count == 2) webuSocket.Disconnect();
		}
	}
}

public class Test_2_2_Two125_126MessageReceivedOnSameFrame : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(TestFrame2.throttleFrame);
    }
	
    public void OnConnect(WebuSocket webuSocket) {
		var message0 = new byte[126];
		for (var i = 0; i < message0.Length; i++) message0[i] = (byte)i;
		
		webuSocket.Send(message0);
		webuSocket.Send(message0);
    }
	
	private int count;
	
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {
		while (0 < datas.Count) {
			var data0 = datas.Dequeue();
			
			if (data0.Length != 126) Debug.LogError("faild to match Test_2_2_Two125_126MessageReceivedOnSameFrame. data0.Length:" + data0.Length);
			
			count++;
			if (count == 2) webuSocket.Disconnect();
		}
	}
}

public class Test_2_3_Two127_127MessageReceivedOnSameFrame : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(TestFrame2.throttleFrame);
    }
	
    public void OnConnect(WebuSocket webuSocket) {
		var message0 = new byte[127];
		for (var i = 0; i < message0.Length; i++) message0[i] = (byte)i;
		
		webuSocket.Send(message0);
		
		var message1 = new byte[127];
		for (var i = 0; i < message1.Length; i++) message1[i] = (byte)i;
		webuSocket.Send(message1);
    }
	
	private int count;
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {
		while (0 < datas.Count) {
			var data0 = datas.Dequeue();
			
			if (data0.Length != 127) Debug.LogError("faild to match Test_2_3_Two127_127MessageReceivedOnSameFrame. data0.Length:" + data0.Length);
			
			count++;
			if (count == 2) webuSocket.Disconnect();
		}
	}
}

public class Test_2_4_Two128_128MessageReceivedOnSameFrame : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(TestFrame2.throttleFrame);
    }
	
    public void OnConnect(WebuSocket webuSocket) {
		var message0 = new byte[128];
		for (var i = 0; i < message0.Length; i++) message0[i] = (byte)i;
		
		webuSocket.Send(message0);
		
		var message1 = new byte[128];
		for (var i = 0; i < message1.Length; i++) message1[i] = (byte)i;
		webuSocket.Send(message1);
    }
	
	private int count;
	
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {
		while (0 < datas.Count) {
			var data0 = datas.Dequeue();
			
			if (data0.Length != 128) Debug.LogError("faild to match Test_2_4_Two128_128MessageReceivedOnSameFrame. data0.Length:" + data0.Length);
			
			count++;
			if (count == 2) webuSocket.Disconnect();
		}
	}
}

public class Test_2_5_LargeSizeMessageReceived : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
	/*
		このパラメータの組み合わせだと、3回に一回くらい再現できる。
		throttle unlimited + 43000byte x 32 message at once -> refrelction server refrects -> fragmentation appears.
	*/
	
	private int size = 43000;// 単純にluajitの耐えられる一回の実行の重さの限界がありそう。このあたりが限界っぽい。10k x 100 / frame
	private int amount = 32;
	
    public void OnConnect(WebuSocket webuSocket) {
		for (var i = 0; i < amount; i++) {
			var message0 = new byte[size];
			for (var j = 0; j < message0.Length; j++) message0[j] = (byte)j;
			webuSocket.Send(message0);
		}
    }
	private int count = 0;
	private int total = 0;
	
    public void OnReceived(WebuSocket webuSocket, Queue<byte[]> datas) {
		while (0 < datas.Count) {
			var data0 = datas.Dequeue();
			
			if (data0.Length != size) Debug.LogError("faild to match Test_2_5_FiveMiddleSizeMessageReceivedOnSameFrame. data0.Length:" + data0.Length);
			
			total += data0.Length;
			// Debug.LogError("total:" + total);
			count++;
			
			if (count == amount) {
				webuSocket.Disconnect();
			}
		}
	}
}