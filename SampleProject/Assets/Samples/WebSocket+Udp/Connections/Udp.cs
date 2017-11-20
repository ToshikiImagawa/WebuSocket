using System;
using System.Net;
using System.Net.Sockets;
using AutoyaFramework.Connections.IP;
using UnityEngine;

/**
	implementation of Udp send/receive.
*/
namespace AutoyaFramework.Connections.Udp {
    /**
        udp receiver feature.
        you MUST Close() when you finish using this receiver.
     */
    public class UdpReceiver {
        private readonly UdpClient udp;
        private readonly IPEndPoint remoteEndPoint;

        private readonly object lockObj;
        private bool closed;

        public UdpReceiver (IPAddress target, int port, Action<byte[]> receiver, IPEndPoint remoteEndPoint=null) {
            var endpoint = new IPEndPoint(target, port);
            
            if (remoteEndPoint != null) {
                this.remoteEndPoint = remoteEndPoint;
            }

            // anyでもいけるのかどうか試すチャンス
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.SendTimeout = 10;

            lockObj = new object();

            ContinueReceive(receiver, endpoint);
        }

        public int Send (byte[] data) {
            if (remoteEndPoint != null) {
                return udp.Send(data, data.Length, remoteEndPoint);
            }
            return 0;
        }

        private void ContinueReceive (Action<byte[]> receiver, IPEndPoint endpoint) {
            udp.BeginReceive(
                ar => {
                    var receivedBytes = udp.EndReceive(ar, ref endpoint);
                    if (receivedBytes.Length == 0) {
                        throw new Exception("receivedBytes is 0.");
                    }

                    if (receiver != null) {
                        receiver(receivedBytes);
                    }

                    if (!closed) {
                        ContinueReceive(receiver, endpoint);
                    }
                }, 
                lockObj
            );
        }

        public void Close () {
            if (closed) {
                return;
            } 

            closed = true;
            udp.Close();
        }
    }

    /**
        udp sender feature.
        you MUST Close() when you finish using this sender.
     */
    public class UdpSender {
        private readonly UdpClient udp;
        private readonly object lockObj;
        private bool closed;

        public UdpSender (IPAddress target, int port) {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Connect(target, port);
            // udp.EnableBroadcast = true;
            
            lockObj = new object();
        }

        public int SendSync (byte[] data) {
            return udp.Send(data, data.Length);
        }

        public void Send (byte[] data) {
            try {
                udp.BeginSend(
                    data, 
                    data.Length, 
                    ar => {
                        Debug.Log("sended. ar:" + ar.IsCompleted);
                        udp.EndSend(ar);
                    }, 
                    lockObj
                );
            } catch (Exception e) {
                Debug.Log("どんなエラーでるのこれ:" + e);
            }
        }

        public void Close () {
            if (closed) {
                return;
            } 

            closed = true;
            udp.Close();
        }
    }
}