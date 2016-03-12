using UnityEngine;
using UnityEditor;

using System;
using System.Threading;


[InitializeOnLoad] public class ServerInitializer {
	static ServerContext sContext;

	static DisqueConnectionController disqueConnectionCont;


	static ServerInitializer () {
		Init();
	}

	public static void Init () {
		sContext = new ServerContext();
		disqueConnectionCont = new DisqueConnectionController(SetupUpdaterThread, "reflector_disque_client_context", DisqueConnectionController.DisqueDataMode.disque_binary.ToString());
		disqueConnectionCont.SetContext(sContext);
	}
	
	public static void SetupUpdaterThread (string loopId, Func<bool> OnUpdate) {
		Action loopMethod = () => {
			try {
				while (true) {
					// run action for update.
					var continuation = OnUpdate();
					if (!continuation) break;
					
					Thread.Sleep(1);
				}
			} catch (Exception e) {
				Debug.LogError("error on thread:" + loopId + " error:" + e);
			}
		};
		
		var thread = new Thread(new ThreadStart(loopMethod));
		thread.Start();
	}
}
