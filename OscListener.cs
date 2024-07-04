using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CoreOSC;
using CoreOSC.Types;

namespace UniversalTrackerMarkers
{
    public class OscListenerCrashedArgs : EventArgs
    {
        public OscListenerCrashedArgs(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    public class OscBooleanMessageReceivedArgs : EventArgs
    {
        public OscBooleanMessageReceivedArgs(string address, bool value)
        {
            Address = address;
            Value = value;
        }

        public string Address { get; }
        public bool Value { get; }
    }

    internal class OscListener
    {
        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
        private Thread _thread;

        private string? _listenAddress;
        private int? _listenPort;

        public event EventHandler? OscListenerCrashed;
        public event EventHandler? OscBooleanMessageReceived;

        public OscListener()
        {
            _thread = new Thread(() => Listen());
            _thread.Name = "OscListenerThread";
            _thread.IsBackground = true;
        }

        public void Start(string listenAddress, int listenPort)
        {
            if (listenPort < 0 || listenPort > 65536)
            {
                throw new ArgumentOutOfRangeException("listenPort");
            }

            _listenAddress = listenAddress;
            _listenPort = listenPort;

            _thread.Start();
        }

        public void Stop()
        {
            _cancelTokenSource.Cancel();
            _thread.Join();
        }

        private void EmitCrashEvent(string message)
        {
            OscListenerCrashed?.Invoke(this, new OscListenerCrashedArgs(message));
        }

        private void Listen()
        {
            if (_listenAddress == null || !_listenPort.HasValue)
            {
                throw new InvalidOperationException("Listen address or port were not defined");
            }

            // step 1: resolve address
            IPHostEntry host;

            try
            {
                host = Dns.GetHostEntry(_listenAddress);
            }
            catch(SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.HostNotFound)
                {
                    EmitCrashEvent($"Could not resolve host '{_listenAddress}'");
                }
                else
                {
                    EmitCrashEvent($"Unexpected error occured while resolving address '{_listenAddress}': {ex.Message}");
                }
                return;
            }

            // step 2: create sockets for every matched address
            ArrayList sockets = new ArrayList();

            try
            {
                foreach (var addr in host.AddressList)
                {
                    var endpoint = new IPEndPoint(addr, _listenPort.Value);

                    var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                    //socket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.ReuseAddress, true);
                    socket.Bind(endpoint);

                    Debug.WriteLine("Created socket on " + endpoint.ToString());

                    sockets.Add(socket);
                }
            }
            catch (Exception ex)
            {
                EmitCrashEvent($"An error occured while creating a socket: {ex.Message}");

                foreach (Socket socket in sockets)
                {
                    socket.Close();
                    socket.Dispose();
                }

                return;
            }

            // step 3: enter select/listen loop on all sockets

            byte[] buffer = new byte[4096]; // allocate enough bytes
            BytesConverter bytesConverter = new BytesConverter();
            OscMessageConverter messageConverter = new OscMessageConverter();

            try
            {
                while (!_cancelTokenSource.IsCancellationRequested)
                {
                    var listenSockets = (ArrayList)sockets.Clone();

                    Socket.Select(listenSockets, null, null, TimeSpan.FromMilliseconds(50));

                    foreach (Socket socket in listenSockets)
                    {
                        int readBytes = socket.Receive(buffer);

                        var dWords = bytesConverter.Serialize(buffer[0..readBytes]);
                        messageConverter.Deserialize(dWords, out var oscMessage);

                        try
                        {
                            ProcessOscMessage(oscMessage);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EmitCrashEvent($"Error in listen thread: {ex.Message}");
                return;
            } finally
            {
                foreach (Socket socket in sockets)
                {
                    socket.Close();
                    socket.Dispose();
                }
            }

            Debug.WriteLine("OscListener exited");
        }

        private void ProcessOscMessage(OscMessage oscMessage)
        {
            if (oscMessage.Arguments.Count() != 1)
            {
                return;
            }

            var argument = oscMessage.Arguments.First();
            bool value;            

            if (argument.GetType() == typeof(OscTrue))
            {
                value = true; 
            }
            else if (argument.GetType() == typeof(OscFalse))
            {
                value = false;
            }
            //else if (argument.GetType() == typeof(int))
            //{
            //    value = (int)argument != 0;
            //}
            else
            {
                return; // ignore
            }

            OscBooleanMessageReceived?.Invoke(this, new OscBooleanMessageReceivedArgs(oscMessage.Address.Value, value));
        }
    }
}
