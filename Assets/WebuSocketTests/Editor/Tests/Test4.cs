using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WebuSocket;

/*
	tests for check correctness of payload.
*/


public class Test_4_0_ReceiveManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "10000DataRequest";
		webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "10000DataRequest") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 10000) {
			webuSocket.Close();
		}
	}
}

public class Test_4_1_ReceiveManyManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(0, null, 60 * 100);
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "100000DataRequest";
		webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "100000DataRequest") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 100000) {
			webuSocket.Close();
		}
	}
}

public class Test_4_2_SendManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "1000DataSendAndReturn";
		for (var i = 0; i < 1000; i++) webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "1000DataSendAndReturn") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 1000) {
			webuSocket.Close();
		}
	}
}

public class Test_4_3_SendManyManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "10000DataSendAndReturn";
		for (var i = 0; i < 10000; i++) webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "10000DataSendAndReturn") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 10000) {
			webuSocket.Close();
		}
	}
}



public class Test_4_4_SendAndReceiveManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(0, null, 60 * 180);
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "10000DataRequest";
		webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
		
		var manyDataRequest2 = "1000DataSend";
		for (var i = 0; i < 1000; i++) webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest2.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "10000DataRequest") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 10000) {
			webuSocket.Close();
		}
	}
}

public class Test_4_5_SendAndReceiveManyManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(0, null, 60 * 180);
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "100000DataRequest";
		webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
		
		var manyDataRequest2 = "10000DataSend";
		for (var i = 0; i < 10000; i++) webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest2.ToCharArray()));
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "100000DataRequest") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 100000) {
			webuSocket.Close();
		}
	}
}

public class Test_4_6_SendAndReceiveAsyncManyTimes : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return new OptionalSettings(0, null, 60 * 180);
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var manyDataRequest = "10000DataRequestAsync";
		webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest.ToCharArray()));
		
		var i = 0;
		
		var manyDataRequest2 = "10000DataSend";
		ServerInitializer.SetupUpdaterThread(
			"10000DataSendThread",
			() => {
				webuSocket.Send(Encoding.UTF8.GetBytes(manyDataRequest2.ToCharArray()));
				i++;
				if (i == 10000) return false;
				return true;
			}
		); 
    }
	
	int count;
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		foreach (var data in datas) {
			var message = Encoding.UTF8.GetString(data);
			if (message == "10000DataRequestAsync") count++;
			else Debug.LogError("message:" + message);
		}
		
		if (count == 10000) {
			webuSocket.Close();
		}
	}
}