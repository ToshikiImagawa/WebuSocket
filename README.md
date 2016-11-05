#WebuSocket

WebSocket Client implementation for C#.  
ver 0.6.7

##motivation

* async default
* runnable on C# 3.5 or later(includes Unity.)

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
* timeout setting
* redirection
* tls 1.3
* else...

contribute welcome!

##license
MIT.

