using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WebuSocket;

/*
	tests for closing.
*/

public class Test_3_0_DisconnectFromServer : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
    public void OnConnect(WebuSocketClient webuSocket) {
		var closeRequest = "closeRequest";
		webuSocket.Send(Encoding.UTF8.GetBytes(closeRequest.ToCharArray()));
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		webuSocket.Close();// サーバから切断するような仕掛けは存在しない、、
	}
}


public class Test_3_1_DisconnectFromClient : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		webuSocket.Close();
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
	}
}

public class Test_3_2_DisconnectWithClose : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		webuSocket.Close();
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		
	}
}

public class Test_3_3_DisconnectWithCloseSync : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		webuSocket.CloseSync();
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		
	}
}

public class Test_3_4_DisconnectWithoutClose : ITestCase {
	public OptionalSettings OnOptionalSettings () {
        return DefaultSetting.Default();
    }
	
    public void OnConnect(WebuSocketClient webuSocket) {
		// 突然の死を演じたい、、
    }
	
    public void OnReceived(WebuSocketClient webuSocket, Queue<byte[]> datas) {
		
	}
}