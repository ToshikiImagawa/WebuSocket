using System;
using System.Collections.Generic;
using UnityEngine;
using WebuSocket;

public interface ITestCase {
	OptionalSettings OnOptionalSettings ();
	void OnConnect (WebuSocketClient webuSocket);
	void OnReceived (WebuSocketClient webuSocket, Queue<byte[]> datas);
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

public class Test_0_0_OrderAndDataShouldMatch : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
        webuSocket.Send(new byte[]{1,2,3});
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data[0] == 1 && data[1] == 2 && data[2] == 3) {
				webuSocket.Close();
			} else {
				Debug.LogError("not match.");
			}
		}
    }
}

public class Test_0_1_SizeMatching_126 : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
		
    public void OnConnect(WebuSocketClient webuSocket) {
		var data126 = new byte[126];
		for (var i = 0; i < data126.Length; i++) data126[i] = 1;
		webuSocket.Send(data126);
    }

    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data.Length != 126) Debug.LogError("not match.");
			webuSocket.Close();
			return;
		}
		Debug.LogError("not match 2.");
    }
}

public class Test_0_2_SizeMatching_127 : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		var data = new byte[127];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);
    }

    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data.Length != 127) Debug.LogError("not match.");
			webuSocket.Close();
			return;
		}
		Debug.LogError("not match 2.");
    }
}


public class Test_0_3_SizeMatching_65534 : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		var data = new byte[65534];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);
    }

    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data.Length != 65534) Debug.LogError("not match.");
			webuSocket.Close();
			return;
		}
		Debug.LogError("not match 2.");
    }
}


public class Test_0_4_SizeMatching_65535 : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		var data = new byte[65535];
		for (var i = 0; i < data.Length; i++) data[i] = 1;
		webuSocket.Send(data);
    }

    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data.Length != 65535) Debug.LogError("not match.");
			webuSocket.Close();
			return;
		}
		Debug.LogError("not match 2.");
    }
}


public class Test_0_5_SizeMatching_14140 : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
		
    public void OnConnect(WebuSocketClient webuSocket) {
		var data = new byte[14140];
		for (var i = 0; i < data.Length; i++) data[i] = (byte)i;
		webuSocket.Send(data);
    }

    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
        if (datas.Count == 1) {
			var data = datas.Dequeue();
			if (data.Length != 14140) Debug.LogError("not match.");
			webuSocket.Close();
			return;
		}
		Debug.LogError("not match 2.");
    }
}