using UnityEditor;
using UnityEngine;
using DisquuunCore;
using System;
using DisquuunCore.Deserialize;
using System.Linq;
using System.Collections.Generic;
using System.Text;

[InitializeOnLoad] public class DummyServer {
    
    static DummyServer () {
        Debug.Log("initialized.");

        Disquuun disquuun = null;

        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
            // pass.            
        } else {
            return;
        }

        // set server handler.
        {
            EditorApplication.CallbackFunction runningCheck = null;

            runningCheck = () => {
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
                    EditorApplication.update -= runningCheck;
                    disquuun.Disconnect();
                    Debug.Log("server disconnected.");
                }
            };

            EditorApplication.update += runningCheck;
        }

        disquuun = new Disquuun(
            "127.0.0.1", 
            7711, 
            1024, 
            5,
            disquuunId => {
                Debug.Log("connected to disque.");
                
                var queueId = "sample_disque_client_context";

                // getJobのループ
                disquuun.GetJob(new string[]{queueId}).Loop(
                    (command, getJobData) => {
                        var jobDatas = DisquuunDeserializer.GetJob(getJobData);
                                
                        // get jobId from got job data.
                        var gotJobIds = jobDatas.Select(jobData => jobData.jobId).ToArray();
                        
                        // fastack it.
                        disquuun.FastAck(gotJobIds).Async(
                            (fastAckCommand, fastAckData) => {
                                // fastack succeded or not.
                                var fastAckedJobCount = DisquuunDeserializer.FastAck(fastAckData);
                                if (fastAckedJobCount != gotJobIds.Length) {
                                    Debug.LogWarning("shortage of fastAckedJobCount:" + fastAckedJobCount + " expected:" + gotJobIds.Length);
                                }
                                // Debug.Log("fastack done.");
                            } 
                        );

                        var datas = jobDatas.Select(job => job.jobData).ToArray();

                        var echoPool = new Dictionary<string, List<byte[]>>();
                        
                        
                        foreach (var data in datas) {
                            switch ((char)data[0]) {
                                /*
                                    STATE_CONNECT			= 1
                                    STATE_STRING_MESSAGE	= 2
                                    STATE_BINARY_MESSAGE	= 3
                                    STATE_DISCONNECT_INTENT	= 4
                                    STATE_DISCONNECT_ACCIDT = 5
                                    STATE_DISCONNECT_DISQUE_ACKFAILED = 6
                                    STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED = 7
                                 */
                                case '1': {
                                    // connected. do nothing.
                                    continue;
                                }
                                case '2': {
                                    // string message.
                                    // pass.
                                    break;
                                }
                                case '3': {
                                    // binary message.
                                    // pass.
                                    break;
                                }
                                case '4': {
                                    // intentional disconnect.
                                    // do nothing.
                                    continue;
                                }
                                case '5': {
                                    // accidentional disconnect.
                                    // do nothing.
                                    continue;
                                }
                                default: {
                                    // some kind of error.
                                    continue;
                                }
                            }
                            
                            // 簡単なエコーなので、そのままデータを送信者に返せればいい。
                            var conId = new byte[36];
                            Buffer.BlockCopy(data, 1, conId, 0, conId.Length);
                            
                            var conIdStr = Encoding.UTF8.GetString(conId);

                            var payloadData = new byte[data.Length - 1 - conId.Length];
                            if (payloadData.Length == 0) {
                                // no data found.
                                continue;
                            }

                            Buffer.BlockCopy(data, 1 + conId.Length, payloadData, 0, payloadData.Length);

                            if (!echoPool.ContainsKey(conIdStr)) {
                                echoPool[conIdStr] = new List<byte[]>();
                            }
                            echoPool[conIdStr].Add(payloadData);
                        }

                        if (!echoPool.Any()) {
                            return true;
                        }

                        // 受け取ったデータをパイプラインに込めて送付
                        var addJobs = new List<DisquuunInput>();

                        foreach (var echoDataByConnection in echoPool) {
                            var targetQueueId = echoDataByConnection.Key;
                            var echoDatas = echoDataByConnection.Value;
                            foreach (var echoData in echoDatas) {
                                addJobs.Add(disquuun.AddJob(targetQueueId, echoData));
                            }
                        }

                        disquuun.Pipeline(addJobs.ToArray()).Execute(
                            (responseCommand, results) => {
                                // do nothing.
                            }
                        );

                        return true;
                    }
                );
            }
        );
    }
}