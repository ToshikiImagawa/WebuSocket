using UnityEngine;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

/**
	Motivations
	
	・async default
		だいたい全部async。書いてなくてもasync。
		syncは無い。
		
	・receive per frame
		WebSocket接続後、
		socket.Receiveとsocket.Sendを1Threadベースで一元化する。
		複数箇所で同時にReceiveしない。また、Receiveに非同期動作を含まない。
	
	・2 threading
		thread1:
			serverからの受信データのqueueと、その後にclientからのqueuedデータの送付を行う。
			
		thread2:
			queueされた受信データを解析、消化する。
			
		遅くなったり詰まったりしても、各threadの特定のフレームが時間的に膨張して、結果他の処理が後ろ倒しになるだけ。
		
	・ordered operation
		外部からのrequestや、内部での状態変化などは、できる限りorderedな形で扱う。
		即時的に動くのはcloseくらい。
		
*/
namespace WebuSocket {
	
	public class WebuSocketClient {
		private static RNGCryptoServiceProvider randomGen = new RNGCryptoServiceProvider();
		
		public readonly string webSocketConnectionId;
		
		private Socket socket;
		private readonly Thread updater;
		
		private const string CRLF = "\r\n";
		private const int HTTP_HEADER_LINE_BUF_SIZE = 1024;
		private const string WEBSOCKET_VERSION = "13"; 
		
			
		public enum WSConnectionState : int {
			Opening,
			Opened,
			Closing,
			Closed
		}
		
		private enum WSOrder : int {
			CloseGracefully,
		}
		
		private Queue<WSOrder> stackedOrders = new Queue<WSOrder>();
		
		private Queue<byte[]> stackedSendingDatas = new Queue<byte[]>();
		
		private Queue<byte[]> receivedDataQueue = new Queue<byte[]>();
		
		private WSConnectionState state;
		
		
		public WebuSocketClient (
			string url,
			Action OnConnected,
			Action<Queue<byte[]>> OnMessage,
			Action<string> OnClosed,
			Action<string> OnError
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			
			state = WSConnectionState.Opening;
			
			/*
				thread for process the queue of received data.
			*/
			var receivedDataQueueProcessor = Updater(
				"WebuSocket-process-thread",
				() => {
					lock (receivedDataQueue) {
						while (0 < receivedDataQueue.Count) {
							if (OnMessage != null) {
								var data = receivedDataQueue.Dequeue();
								// １通とは限らない。複数が入ってる可能性のほうが高い。
			
								// 長さが書いてあるんでここで分割する + payloadごとに割る。
								// 種類を分解して、コントロールならコントロール、そうでないなら、、みたいなことをする。
								
								OnMessage(new Queue<byte[]>());
							}
						}
						return true;
					}
				}
			);
			
			var frame = 0;
			
			/*
				main thread for websocket data receiving & sending.
			*/
			updater = Updater(
				"WebuSocket-main-thread",
				() => {
					switch (state) {
						case WSConnectionState.Opening: {
							var newSocket = WebSocketHandshake(url, OnError);
							
							if (newSocket != null) {
								this.socket = newSocket;
								
								state = WSConnectionState.Opened;
								
								if (OnConnected != null) OnConnected();
								break;
							}
							
							// handshake connection failed.
							// OnError handler is already fired.
							return false;
						}
						case WSConnectionState.Opened: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									lock (receivedDataQueue) receivedDataQueue.Enqueue(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							
							lock (stackedSendingDatas) {
								while (state == WSConnectionState.Opened && 0 < stackedSendingDatas.Count) {
									// queueに入ってるもの全部繋げて送信ってやっていいような気がするんだが、まあちゃんとやるか、、
									var data = stackedSendingDatas.Dequeue();
									
									// websocketのフォーマッティングを行う。さて。binaryかstringかとかあるのか。
									
									// socket.Send
								}
							} 
							
							break;
						}
						case WSConnectionState.Closing: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									lock (receivedDataQueue) receivedDataQueue.Enqueue(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							break;
						}
						case WSConnectionState.Closed: {
							// break queue processor thread.
							receivedDataQueueProcessor.Abort();
							
							// break this thread.
							return false;
						}
					}
					frame++;
					return true;
				},
				OnClosed
			);
		}
		
		
		/*
			public methods.
		*/
		public WSConnectionState State () {
			switch (state){
				case WSConnectionState.Opening: return WSConnectionState.Opening;
				case WSConnectionState.Opened: return WSConnectionState.Opened;
				case WSConnectionState.Closing: return WSConnectionState.Closing;
				case WSConnectionState.Closed: return WSConnectionState.Closed;
				default: throw new Exception("unhandled state.");
			}
		}
		
		public bool IsConnected () {
			switch (state){
				case WSConnectionState.Opened: {
					if (socket != null) {
						if (socket.Connected) return true; 
					}
					return false;
				}
				default: return false;
			}
		}
		
		public void Send (byte[] data) {
			switch (state) {
				case WSConnectionState.Opened: {
					StackData(data);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", send operation request is ignored.");
					break;
				}
			}
		}
		
		
		public void Close () {
			switch (state) {
				case WSConnectionState.Opened: {
					StackOrder(WSOrder.CloseGracefully);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", close operation request is ignored.");
					break;
				}
			}
		}
		
		/*
			forcely close socket on this time.
		*/
		public void CloseSync () {
			ForceClose();
		}
		
		
		
		/*
			private methods.
		*/
		
		private void StackData (byte[] data) {
			lock (stackedSendingDatas) stackedSendingDatas.Enqueue(data); 
		}
		
		private void StackOrder (WSOrder order) {
			lock (stackedOrders) stackedOrders.Enqueue(order);
		}
		
		
		private void ExecuteOrder (WSOrder order) {
			switch (order) {
				case WSOrder.CloseGracefully: {
					state = WSConnectionState.Closing;
					
					// this order is final one of this thread. ignore all other orders.
					stackedOrders.Clear();
					
					var data = WebSocketByteGenerator.CloseData();
					TrySend(data);
					ForceClose();
					break;
				}
				default: {
					Debug.LogError("unhandled order:" + order);
					break;
				}
			}
		}
		
		
		private void TrySend (byte[] data, Action<string> OnError=null) {
			try {
				socket.Send(data);
			} catch (Exception e0) {
				if (OnError != null) OnError("TrySend failed:" + e0 + ". attempt to close forcely.");
				ForceClose(OnError);
			}
		}
		
		private void ForceClose (Action<string> OnError=null, Action<string> OnClosed=null) {
			if (state == WSConnectionState.Closed) {
				if (OnError != null) OnError("already closed.");
				return;
			}
			
			if (state == WSConnectionState.Opening) {
				StackOrder(WSOrder.CloseGracefully);
				return;
			}
			
			if (socket == null) {
				if (OnError != null) OnError("not yet connected or already closed.");
				return;
			}
			
			if (!socket.Connected) {
				if (OnError != null) OnError("connection is already closed.");
				return;
			}
			
			lock (socket) {
				try {
					socket.Close();
				} catch (Exception e) {
					if (OnError != null) OnError("socket closing error:" + e);
				} finally {
					socket = null;
				}
				
				state = WSConnectionState.Closed;
			}
		}
		
		
		private Socket WebSocketHandshake (string urlSource, Action<string> OnError=null) {
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			var agent = "testing_webuSocket_client";
			var base64Key = GeneratePrivateBase64Key();
			
			Debug.LogError("wss and another features are not supported yet.");
			/*
				unsupporteds:
					wss,
					redirect,
					proxy,
					fragments(fin != 1) for sending & receiving.
			*/
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION},
				{"User-Agent", agent}
			};
			
			/*
				construct request bytes data.
			*/
			var requestData = new StringBuilder();
			
			requestData.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			requestData.Append (CRLF);

			var entity = string.Empty;
			requestData.Append(entity);
			
			var requestDataBytes = Encoding.UTF8.GetBytes(requestData.ToString().ToCharArray());
			
			/*
				ready connection sockets.
			*/
			var timeout = 1000;
			
			var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.NoDelay = true;
			sock.SendTimeout = timeout;
			
			Action ForceCloseSock = () => {
				if (socket == null || !socket.Connected) {
				if (OnError != null) OnError("not yet connected or already closed.");
				return;
			}
			
			lock (socket) {
				try {
					socket.Close();
				} catch (Exception e) {
					if (OnError != null) OnError("socket closing error:" + e);
				} finally {
					socket = null;
				}
				
				state = WSConnectionState.Closed;
			}
			};
			
			try {
				sock.Connect(host, port);
			} catch (Exception e) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to connect to host:" + host + " error:" + e);
				return null;
			}
			
			if (!sock.Connected) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to connect.");
				return null;
			}
			
			try {
				var result = sock.Send(requestDataBytes);
				
				if (0 < result) {}// succeeded to send.
				else {
					ForceCloseSock();
					if (OnError != null) OnError("failed to send handshake request data, send size is 0.");
					
					return null;
				}
			} catch (Exception e) {
				
				ForceCloseSock();
				if (OnError != null) OnError("failed to send handshake request data. error:" + e);
				
				return null;
			}
			
			
			
			/*
				read connection response from socket.
			*/
			var responseHeaderDict = new Dictionary<string, string>();
			{
				/*
					protocol should be switched.
				*/
				var protocolResponse = ReadLineBytes(sock);
				if (!string.IsNullOrEmpty(protocolResponse.error)) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to receive response.");
					return null;
				}
				
				if (Encoding.UTF8.GetString(protocolResponse.data).ToLower() != "HTTP/1.1 101 Switching Protocols".ToLower()) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to switch protocol.");
					return null;
				}
				
				if (sock.Available == 0) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to receive rest of response header.");
					return null;
				}
				
				/*
					rest data exists and can be received.
				*/
				while (0 < sock.Available) {
					var responseHeaderLineBytes = ReadLineBytes(sock);
					if (!string.IsNullOrEmpty(responseHeaderLineBytes.error)) {
						ForceCloseSock();
						if (OnError != null) OnError("responseHeaderLineBytes.error:" + responseHeaderLineBytes.error);
						return null;
					}
					
					var responseHeaderLine = Encoding.UTF8.GetString(responseHeaderLineBytes.data);
					
					if (!responseHeaderLine.Contains(":")) continue;
					
					var splittedKeyValue = responseHeaderLine.Split(':');
					
					var key = splittedKeyValue[0].ToLower();
					var val = splittedKeyValue[1];
					
					responseHeaderDict[key] = val;
				}
			}
				
			// validate.
			if (!responseHeaderDict.ContainsKey("Server".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Server' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Date".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Date' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Connection".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Connection' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Upgrade".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Upgrade' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Sec-WebSocket-Accept".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Sec-WebSocket-Accept' key.");
				return null;
			}
			var serverAcceptedWebSocketKey = responseHeaderDict["Sec-WebSocket-Accept".ToLower()];
			
			
			if (!sock.Connected) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to check connected after validate.");
				return null;
			}
			
			return sock;
		}
		
		private byte[] httpResponseReadBuf = new byte[HTTP_HEADER_LINE_BUF_SIZE];

		public ResponseHeaderLineDataAndError ReadLineBytes (Socket sock) {
			byte[] b = new byte[1];
			
			var readyReadLength = sock.Available;
			if (httpResponseReadBuf.Length < readyReadLength) {
				new ResponseHeaderLineDataAndError(new byte[0], "too long data for read as line found");
			} 
			
			/*
				there are not too long data.
				"readyReadLength <= BUF_SIZE".
				
				but it is not surpported that this socket contains data which containes "\n".
				cut out when buffering reached to full.
			*/
			int i = 0;
			while (true) {
				sock.Receive(b);
				
				if (b[0] == '\r') continue;
				if (b[0] == '\n') break;
				
				httpResponseReadBuf[i] = b[0];
				i++;
				
				if (i == readyReadLength) {
					Debug.LogError("no \n appears. cut out.");
					break;
				}
			}
			
			var retByte = new byte[i];
			Array.Copy(httpResponseReadBuf, 0, retByte, 0, i);

			return new ResponseHeaderLineDataAndError(retByte);
		}
		
		public struct ResponseHeaderLineDataAndError {
			public readonly byte[] data;
			public readonly string error;
			public ResponseHeaderLineDataAndError (byte[] data, string error=null) {
				this.data = data;
				this.error = error;
			}
		}
		
		private static string GeneratePrivateBase64Key () {
			var src = new byte[16];
			randomGen.GetBytes(src);
			return Convert.ToBase64String(src);
		}
		
		public static byte[] NewMaskKey () {
			var maskingKeyBytes = new byte[4];
			randomGen.GetBytes(maskingKeyBytes);
			return maskingKeyBytes;
		}
		
		private Thread Updater (string loopId, Func<bool> OnUpdate, Action<string> OnClosed=null) {
			Action loopMethod = () => {
				try {
					while (true) {
						// run action for update.
						var continuation = OnUpdate();
						if (!continuation) break;
						
						Thread.Sleep(1);
					}
					
					if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " is finished gracefully.");
				} catch (Exception e) {
					if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " finished with error:" + e);
				}
			};
			
			var thread = new Thread(new ThreadStart(loopMethod));
			thread.Start();
			return thread;
		}
		
	}
}
