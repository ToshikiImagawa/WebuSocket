using UnityEngine;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

/**
	Motivations
	
	・receive per frame
		WebSocket接続後、
		socket.receiveをフレームレートベースで一元化する。複数箇所で同時にreceiveしない。また、receiveに非同期invokeを含まない。
		これを守らないと高負荷時にデータがおかしくなる。
	
	・frame based single threading
		フレームベースだが別threadなので、本体には影響を出さない。
		遅くなったり詰まったりするとしたら、threadの特定のフレームが時間的に膨張して、結果他の処理が後ろ倒しになる。
		データ取得に関しては、pullベースだとこいつが応答しない時に影響出そうだな〜って感じなのでやはりpushを考えよう。
		
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
		// private Queue<byte[]> stackedReceivedPrivateDatas = new Queue<byte[]>();	
		
		
		private WSConnectionState state;
		
		
		public WebuSocketClient (
			string url,
			Action OnConnected=null,
			Action<Queue<byte[]>> OnMessage=null,
			Action OnClosing=null,
			Action OnClosed=null,
			Action OnError=null
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			
			state = WSConnectionState.Opening;
			
			var frame = 0;
			
			// thread for websocket.
			updater = Updater(
				"WebuSocket",
				() => {
					switch (state) {
						case WSConnectionState.Opening: {
							var socketComponent = WebSocketHandshake(url, OnError);
							if (socketComponent != null) {
								state = WSConnectionState.Opened;
								this.socket = socketComponent.socket;
								
								if (OnConnected != null) OnConnected();
								break;
							}
							
							// connection failed.
							ForceClose();
							
							break;
						}
						case WSConnectionState.Opened: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									EnqueueReceivedData(buff);
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
									
									// websocketのフォーマッティングを行う。さて。
									
									// socket.Send
									
									// この途中でおっちんでも平気だと思うんだよな。
								}
							} 
							
							if (frame == 100) CloseAsync();
							break;
						}
						case WSConnectionState.Closing: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									EnqueueReceivedData(buff);
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
							if (OnClosed != null) OnClosed();
							
							// break this thread.
							return false;
						}
					}
					frame++;
					return true;
				}
			);
		}
		
		
		public void CloseAsync () {
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
		public void Close () {
			ForceClose();
		}
		
		
		
		private void EnqueueReceivedData (byte[] receivedData) {
			// １通とは限らない。複数が入ってる可能性のほうが高い。
			
			// 長さが書いてあるんでここで分割する + payloadごとに割ることは可能
			// 遅いかどうかでいうとどうだろ、、ただ、コントロールであるならここで処理しないといけない。
			// ・コントロールかどうかまではここで見極める。コントロールならExecuteしちゃっていいはず。
			// ・あとはまあ、、しょうがねーか、長さも見る。
			
			
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
		
		
		private void TrySend (byte[] data) {
			try {
				socket.Send(data);
			} catch (Exception e0) {
				Debug.LogError("TrySend failed:" + e0 + ". attempt to close forcely.");
				ForceClose();
			}
		}
		
		private void ForceClose () {
			if (socket == null || !socket.Connected) {
				Debug.LogError("not yet connected or already closed.");
				return;
			}
			
			lock (socket) {
				if (state == WSConnectionState.Closed) {
					Debug.LogError("already closed.");
					return;
				}
				
				try {
					// socket.Shutdown(SocketShutdown.Send);
					// socket.Disconnect(false);
					socket.Close();
				} catch (Exception e) {
					Debug.LogError("socket closing error:" + e);
				} finally {
					socket = null;
				}
				
				state = WSConnectionState.Closed;
			}
		}
		
		// 2つの問題に分ける。
		
		// 1.queueにはデータ以外にもコントロール系のものが入ってきてる
		// 2.queueに入ったメッセージ系データの取り出しが必須
		
		// これ、入れるときに判別しちゃえばいいよな。
		
		// ユーザーが受け取るべきキューに入ったデータについては、ユーザーに送る。
		
		private SocketComponent WebSocketHandshake (string urlSource, Action OnError) {
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
			
			try {
				sock.Connect(host, port);
			} catch (Exception e) {
				Debug.LogError("failed to connect to host:" + host + " error:" + e);
				OnError();
				
				return null;
			}
			
			if (!sock.Connected) {
				Debug.LogError("failed to connect.");
				
				ForceClose();
				OnError();
				
				return null;
			}
			
			try {
				var result = sock.Send(requestDataBytes);
				
				if (0 < result) {}// succeeded to send.
				else {
					Debug.LogError("failed to send connection request data, send size is 0.");
					
					ForceClose();
					OnError();
					
					return null;
				}
			} catch (Exception e) {
				Debug.LogError("failed to send connection request data. error:" + e);
				
				ForceClose();
				OnError();
				
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
					Debug.LogError("failed to receive response.");
					return null;
				}
				
				if (Encoding.UTF8.GetString(protocolResponse.data).ToLower() != "HTTP/1.1 101 Switching Protocols".ToLower()) {
					Debug.LogError("failed to switch protocol.");
					return null;
				}
				
				if (sock.Available == 0) {
					Debug.LogError("failed to receive rest of response header.");
					return null;
				}
				
				/*
					rest data exists and can be received.
				*/
				while (0 < sock.Available) {
					var responseHeaderLineBytes = ReadLineBytes(sock);
					if (!string.IsNullOrEmpty(responseHeaderLineBytes.error)) {
						Debug.LogError("responseHeaderLineBytes.error:" + responseHeaderLineBytes.error);
						break;
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
				Debug.LogError("failed to receive 'Server' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Date".ToLower())) {
				Debug.LogError("failed to receive 'Date' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Connection".ToLower())) {
				Debug.LogError("failed to receive 'Connection' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Upgrade".ToLower())) {
				Debug.LogError("failed to receive 'Upgrade' key.");
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Sec-WebSocket-Accept".ToLower())) {
				Debug.LogError("failed to receive 'Sec-WebSocket-Accept' key.");
				return null;
			}
			var serverAcceptedWebSocketKey = responseHeaderDict["Sec-WebSocket-Accept".ToLower()];
			
			
			if (!sock.Connected) {
				Debug.LogError("disconnected,");
				return null;
			}
			
			return new SocketComponent(sock, base64Key, serverAcceptedWebSocketKey);
		}
		
		public class SocketComponent {
			public readonly Socket socket;
			public readonly string base64Key;
			public readonly string acceptedKey;
			public SocketComponent (Socket socket, string base64Key, string acceptedKey) {
				this.socket = socket;
				this.base64Key = base64Key;
				this.acceptedKey = acceptedKey;
			}
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
		
		private Thread Updater (string loopId, Func<bool> OnUpdate) {
			var framePerSecond = 100;// fixed to 100fps.
			var mainThreadInterval = 1000f / framePerSecond;
			
			Action loopMethod = () => {
				try {
					double nextFrame = (double)System.Environment.TickCount;
					
					var before = 0.0;
					var tickCount = (double)System.Environment.TickCount;
					
					while (true) {
						tickCount = System.Environment.TickCount * 1.0;
						if (nextFrame - tickCount > 1) {
							Thread.Sleep((int)(nextFrame - tickCount)/2);
							/*
								waitを半分くらいにすると特定フレームで安定した。
								まるっきりよく無い。なんか指標があるんだと思うんだけど。
							*/
							continue;
						}
						
						if (tickCount >= nextFrame + mainThreadInterval) {
							nextFrame += mainThreadInterval;
							continue;
						}
						
						// run action for update.
						var continuation = OnUpdate();
						if (!continuation) break;
						
						nextFrame += mainThreadInterval;
						before = tickCount; 
					}
					
					Debug.Log("loopId:" + loopId + " is finished.");
				} catch (Exception e) {
					Debug.LogError("loopId:" + loopId + " error:" + e);
				}
			};
			
			var thread = new Thread(new ThreadStart(loopMethod));
			thread.Start();
			return thread;
		}
		
	}
}
