#WebuSocket

WebSocket Client implementation for C#.  
ver 0.6.5

##motivation

* async default
* runnable on C# 3.5 or later(contains Unity.)

##usage

```C#
var webuSocket = new WebuSocket(
	"ws://somewhere:someport",
	1024 * 100,// default buffer size.
	() => {
		// connected.
	}, 
	(Queue<ArraySegment<byte>> datas) => {
		// data received.
		
		while (0 < datas.Count) {
			var data = datas.Dequeue();
			var bytes = new byte[data.Count];
			Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
			
			// use "bytes".
		}
	}, 
	() => {
		// pinged.
	}, 
	closeReason => {
		// closed.
	}, 
	(errorReason, e) => {
		// error happedned.
	}, 
	customHeaderKeyValues // Dictionary<string, string> which can send with connecting signal.
);
```			

##not yet implemented
* wss
* timeout setting
* redirection
* else...

contribute welcome!

##license
MIT.

