using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WebuSocket {
	public static class WebSocketByteGenerator {
		/*
			send 125	ok
			send 126	ok
			send 127	not yet impl.
			
			read 125	ok
			read 126	ok
			read 127	not yet impl.
		*/
		
		// #0                   1                   2                   3
		// #0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		// #+-+-+-+-+-------+-+-------------+-------------------------------+
		// #|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
		// #|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
		// #|N|V|V|V|       |S|             |   (if payload len==126/127)   |
		// #| |1|2|3|       |K|             |                               |
		// #+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
		// #|     Extended payload length continued, if payload len == 127  |
		// #+ - - - - - - - - - - - - - - - +-------------------------------+
		// #|                               | Masking-key, if MASK set to 1 |
		// #+-------------------------------+-------------------------------+
		// #| Masking-key (continued)       |          Payload Data         |
		// #+-------------------------------- - - - - - - - - - - - - - - - +
		// #:                     Payload Data continued ...                :
		// #+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
		// #|                     Payload Data continued ...                |

		public const byte OP_CONTINUATION	= 0x0; //unsupported.
		public const byte OP_TEXT			= 0x1;// 0001 unsupported.
		public const byte OP_BINARY			= 0x2;// 0010
		public const byte OP_CLOSE			= 0x8;// 1000
		public const byte OP_PING			= 0x9;// 1001
		public const byte OP_PONG			= 0xA;// 1010
		
		private const byte OPFilter			= 0xF;// 1111
		private const byte Length7Filter	= 0xBF;// 01111111
		
		public static byte[] Ping () {
			return WSDataFrame(1, 0, 0, 0, OP_PING, 1, new byte[0]);
		}
		
		public static byte[] Pong () {
			return WSDataFrame(1, 0, 0, 0, OP_PONG, 1, new byte[0]);
		}
		
		public static byte[] SendBinaryData (byte[] data) {
			return WSDataFrame(1, 0, 0, 0, OP_BINARY, 1, data);
		}
				
		public static byte[] CloseData () {
			return WSDataFrame(1, 0, 0, 0, OP_CLOSE, 1, new byte[0]);
		}
		
		private static byte[] WSDataFrame (
			byte fin,
			byte rsv1,
			byte rsv2,
			byte rsv3,
			byte opCode,
			byte mask,
			byte[] data)
		{
			uint length = (uint)(data.Length);
			
			byte dataLength7bit = 0;
			UInt16 dataLength16bit = 0;
			uint dataLength64bit = 0;
			
			if (length < 126) {
				dataLength7bit = (byte)length;
			} else if (length < 65536) {
				dataLength7bit = 126;
				dataLength16bit = (UInt16)length;
			} else {
				Debug.LogError("not yet done this size:" + length);
				dataLength7bit = 127;
				dataLength64bit = length;
			}
			
			/*
				ready data stream structure for send.
			*/
			using (var dataStream = new MemoryStream()) { 
				dataStream.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
				dataStream.WriteByte((byte)((mask << 7) | dataLength7bit));
				
				if (0 < dataLength16bit) {
					var intBytes = new byte[2];
					intBytes[0] = (byte)(dataLength16bit >> 8);
					intBytes[1] = (byte)dataLength16bit;
					
					// dataLength16 to 2byte.
					dataStream.Write(intBytes, 0, intBytes.Length);
				}
				if (0 < dataLength64bit) {
					// dataLength64 to 8byte.
					// dataStream.WriteByte();
				}
				
				// should mask control frame.
				var maskKey = WebuSocketClient.NewMaskKey();
				dataStream.Write(maskKey, 0, maskKey.Length);
				
				// mask data.
				var maskedData = data.Masked(maskKey);
				dataStream.Write(maskedData, 0, maskedData.Length);
				
				return dataStream.ToArray();
			}
		}
		
		private static byte[] Masked (this byte[] data, byte[] maskKey) {
			for (var i = 0; i < data.Length; i++) data[i] ^= maskKey[i%4];
			return data;
		}
		
		public static List<OpCodeAndPayload> SplitData (byte[] data) {
			var opCodeAndPayloads = new List<OpCodeAndPayload>();
			
			uint cursor = 0;
			while (cursor < data.Length) {
				// first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
				var opCode = (byte)(data[cursor++] & OPFilter);
				
				// second byte = mask(1), length(7)
				/*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
				uint length = (uint)data[cursor++];
				switch (length) {
					case 126: {
						// next 2 byte is length data.
						length = (uint)(
							(data[cursor++] << 8) +
							(data[cursor++])
						);
						break;
					}
					case 127: {
						// next 8 byte is length data.
						Debug.LogError("読み込む2。未実装。");
						break;
					}
					default: {
						break;
					}
				}
				
				if (length == 0) {//サイズ読むのに失敗するとここにきちゃうな、、
					opCodeAndPayloads.Add(new OpCodeAndPayload(opCode));
					continue;
				}
				
				var payload = new byte[length];
				Array.Copy(data, cursor, payload, 0, payload.Length);
				cursor = cursor + length;
				
				opCodeAndPayloads.Add(new OpCodeAndPayload(opCode, payload));
			}
			
			return opCodeAndPayloads;
		}
		
		public struct OpCodeAndPayload {
			public readonly byte opCode;
			public readonly byte[] payload;
			public OpCodeAndPayload (byte opCode, byte[] payload=null) {
				this.opCode = opCode;
				this.payload = payload;
			}
		}
	}
}