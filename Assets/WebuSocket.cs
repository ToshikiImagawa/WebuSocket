using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace WebuSocket {
	
	public class WebuSocketClient {
		private Socket socket;
		
		public WebuSocketClient (string protocol, string host, int port) {
			var url = protocol + "://" + host + ":" + port;
			// wsかwss、まずはws protocolだけサポートする。いきなり非同期でいいんじゃねーかな。
			Debug.LogError("url:" + url);
			
			
			Debug.LogError("非同期前提にする。なので、ここでconnectingにして返す。");
			state = ReadyState.Connecting;
			
			// threadを作って放置
			Updater(
				"WebuSocket",
				() => {
					switch (state) {
						case ReadyState.Connecting: {
							WebSocketHandshakeRequestFromClient(url);
							
							// TCPConnect(host, port);
							
							Debug.LogError("終わったらOpenとかに変わってるはず");
							state = ReadyState.Closed;
							break;
						}
					} 
					return true;
				}
			);
		}
		
		
		
		private void WebSocketHandshakeRequestFromClient (string urlSource) {
			Debug.LogError("ダミーパラメータ一杯");
			
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var url = uri.PathAndQuery;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			var agent = "testing webuSocket client";
			var base64Key = GeneratePrivateBase64Key();
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION}
			};
			
			// stream.Write(buff, 0, buff.Length);
		}
		
		private static string GeneratePrivateBase64Key () {
			var randomGen = new RNGCryptoServiceProvider();
			
			var src = new byte[16];
			randomGen.GetBytes(src);
			return Convert.ToBase64String(src);
		}
		
		private void Updater (string loopId, Func<bool> OnUpdate) {
			var framePerSecond = 60;
			var mainThreadInterval = 1000f / framePerSecond;
			
			Action loopMethod = () => {
				try {
					double nextFrame = (double)System.Environment.TickCount;
					
					var before = 0.0;
					var tickCount = (double)System.Environment.TickCount;
					
					while (true) {
						tickCount = System.Environment.TickCount * 1.0;
						if (nextFrame - tickCount > 1) {
							// XrossPeer.Log("wait:" + (int)(nextFrame - tickCount));
							Thread.Sleep((int)(nextFrame - tickCount)/2);
							/*
								waitを半分くらいにすると特定フレームで安定した。
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
		}
		
		
		private void TCPConnect (string host, int port) {
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.socket.NoDelay = true;
            
			int timeout = 1000;// うーん怖い
			
            this.socket.SendTimeout = timeout;
			
			try {
				this.socket.Connect(host, port);
			} catch (Exception e) {
				Debug.Log("ERROR: WebuSocketClient: failed to connect to server @:" + host + ":" + port);
				Debug.Log("ERROR: reason:" + e);
			}

			if (!this.socket.Connected) {
				// failed to create connection. 
				this.socket.Close();
				this.socket = null;
				throw new Exception("failed to connect to server @:" + host + ":" + port);
			}
			
			Debug.Log("succeedet to connect. ただしTCP");
			
			// self.listening = True
			// while self.listening:
			// 	try:
			// 		# when teardown, causes close then "Software caused connection abort"
			// 		(conn, addr) = self.socket.accept()

			// 		identity = str(uuid.uuid4())

			// 		# genereate new client
			// 		client = WSClient(self, identity)
					
			// 		self.clientIds[identity] = client

			// 		threading.Thread(target = client.handle, args = (conn,addr)).start()
			// 	except socket.error as msg:
			// 		errorMsg = "SublimeSocket WebSocketServing crashed @ " + str(self.host) + ":" + str(self.port) + " reason:" + str(msg)
			// 		self.sublimeSocketServer.transferNoticed(errorMsg)
		}
		
		
		
		private const int BUF_SIZE = 1024;
		private const string WEBSOCKET_VERSION = "13"; 
		private const string WEBSOCKET_MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
		
		public enum ReadyState : int {
			Connecting,
			Open,
			Closing,
			Closed
		}
		
		private ReadyState state;
		
		private void HandShake () {
		// def handshake(self):
			var headers = new Dictionary<string, string>();
			
			// # Ignore first line with GET
			var line = ReadLineHeader();

			while (state == ReadyState.Connecting) {//self.hasStatus('CONNECTING'):
				if (64 < headers.Count) throw new Exception("Header is too long.");
					
				line = ReadLineHeader();
				
				// 接続中状態を維持してなければ離れる。
				if (state != ReadyState.Connecting) throw new Exception("Client is left.");
				
				if (line.Length == 0 || line.Length == BUF_SIZE) throw new Exception("Invalid line in header.");
				
				// detect read end.
				if (line == "\r\n") break;

				// 前後の空白削ってる
				// line = line.strip()
				
				var kv = line.Split(':');// 一個以上:がある場合マズイ。
				
				if (kv.Length == 2) {
					var k = kv[0].ToLower();// ここでも前後の空白削るのやってる
					var v = kv[1];// ここでも前後の空白削るのやってる
					headers[k] = v;
				} else {
					throw new Exception("Invalid header key/value.");
				}
			}

			// validate exists or not.
			if (!headers.Any()) throw new Exception("Reading headers failed.");
			if (!headers.ContainsKey("sec-websocket-version")) throw new Exception("Missing parameter \"Sec-WebSocket-Version\".");
			if (!headers.ContainsKey("sec-websocket-key")) throw new Exception("Missing parameter \"Sec-WebSocket-Key\".");
			if (!headers.ContainsKey("host")) throw new Exception("Missing parameter \"Host\".");
			if (!headers.ContainsKey("origin")) throw new Exception("Missing parameter \"Origin\".");
			
			// validate protocol.
			if (headers["sec-websocket-version"] != WEBSOCKET_VERSION) throw new Exception("Wrong protocol version:" + WEBSOCKET_VERSION);
			
			
			var key = headers["sec-websocket-key"];
			Debug.LogError("ここまでで、keyが取得できているはず key:" + key);
			// var accept = base64.b64encode(
			// 		(
			// 			hashlib.sha1(
			// 				(key + WEBSOCKET_MAGIC).encode('utf-8')
			// 			)
			// 		).digest()
			// 	);
			// var decodedAccept = accept.decode('utf-8');

			// currentBytes = ('HTTP/1.1 101 Switching Protocols\r\n'
			// 	'Upgrade: websocket\r\n'
			// 	'Connection: Upgrade\r\n'
			// 	'Sec-WebSocket-Origin: %s\r\n'
			// 	'Sec-WebSocket-Location: ws://%s\r\n'
			// 	'Sec-WebSocket-Accept: %s\r\n'
			// 	'Sec-WebSocket-Version: %s\r\n'
			// 	'\r\n') % (headers['origin'], headers['host'], decodedAccept, headers['sec-websocket-version'])

			// handshakeMessage = '--- HANDSHAKE ---\r\n'
			// handshakeMessage = handshakeMessage + '-----------------\r\n'
			// handshakeMessage = handshakeMessage + currentBytes + '\r\n'
			// handshakeMessage = handshakeMessage + '-----------------\r\n'
			
			// bufferdBytes = bytes(currentBytes, 'utf-8')
			// self.send(bufferdBytes)
		}
		
		private byte[] Receive (int bufSize) {
			// def receive(self, bufsize):
			try {
				var bytes = new byte[100];//self.conn.recv(bufsize) //ここでConnectionが露出する。
				return bytes;
			} catch (Exception e) {
				Debug.LogError("Receive e:" + e);
				return new byte[0]; 
			}
		}
		
		private string ReadLineHeader () {
			var line = string.Empty;

			while (state == ReadyState.Connecting && line.Length < BUF_SIZE) {
				var c = Receive(1);// byte to charが必要、たぶんコードでいいはず。
				line = line + Encoding.UTF8.GetString(c);

				if (c[0] == '\n') {
					Debug.LogError(" _\\_ n_ であってた");
					break;
				}
			}
			return line;
		}
	}
}
