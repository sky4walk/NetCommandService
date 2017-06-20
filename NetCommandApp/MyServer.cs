//André Betz 2004
// http://www.andrebetz.de
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Text;
using System.Threading;

namespace NetCommandService
{
	public class StateObject
	{
		public Socket workSocket = null;	// Client socket.
		public Socket partnerSocket = null;	// Partner socket.
		public const int BufferSize = 1024;	// Size of receive buffer.
		public byte[] buffer = new byte[BufferSize];// Receive buffer.
		public DateTime TimeStamp;
	}

	public delegate void SocketReceiver(string sb);
	/// 
	/// Description: MyServer is the class to control sockets	/// 
	public class MyServer
	{
		protected int portNumber;
		protected int maxSockets;
		protected int sockCount = 0;
		private Timer lostTimer;
		private const int numThreads = 1;
		private const int timerTimeout = 300000;
		private const int timeoutMinutes = 3;
		private bool ShuttingDown = false;
		protected string title;
		protected Hashtable connectedHT = new Hashtable();
		protected ArrayList connectedSocks;

		//Thread signal.
		private ManualResetEvent allDone = new ManualResetEvent(false);
		private Thread[] serverThread = new Thread[numThreads];
		private AutoResetEvent[] threadEnd = new AutoResetEvent[numThreads];
		private SocketReceiver m_ReceiveFunc = null;

		public MyServer(int port, string title, SocketReceiver ReceiveFunc)
		{
			this.portNumber = port;
			this.title = title;
			this.maxSockets =10000;
			m_ReceiveFunc = ReceiveFunc;
			connectedSocks = new ArrayList(this.maxSockets);
		}

		/// 
		/// Description: Start the threads to listen to the port and process
		/// messages.
		/// 
		public void Start()
		{
			// Clear the thread end events
			for (int lcv = 0; lcv < numThreads; lcv++)
				threadEnd[lcv] = new AutoResetEvent(false);

			ThreadStart threadStart1 = new ThreadStart(StartListening);
			serverThread[0] = new Thread(threadStart1);
			serverThread[0].IsBackground = true;
			serverThread[0].Start();

			// Create the delegate that invokes methods for the timer.
			TimerCallback timerDelegate = new               TimerCallback(this.CheckSockets);
			/// Create a timer that waits one minute, then invokes every 5 minutes.
			lostTimer = new Timer(timerDelegate, null, MyServer.timerTimeout, MyServer.timerTimeout);

		}

		/// 
		/// Description: Check for dormant sockets and close them.
		/// 
		/// <param name="eventState">Required parameter for a timer call back
		/// method.</param>
		private void CheckSockets(object eventState)
		{
			lostTimer.Change(System.Threading.Timeout.Infinite,
				System.Threading.Timeout.Infinite);
			try
			{
				foreach (StateObject state in connectedSocks)
				{
					if (state.workSocket == null)
					{	// Remove invalid state object
						Monitor.Enter(connectedSocks);
						if (connectedSocks.Contains(state))
						{
							connectedSocks.Remove(state);
							Interlocked.Decrement(ref sockCount);
						}
						Monitor.Exit(connectedSocks);
					}
					else
					{
						if (DateTime.Now.AddTicks(-state.TimeStamp.Ticks).Minute > timeoutMinutes)
						{
							RemoveSocket(state);
						}
					}
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				lostTimer.Change(MyServer.timerTimeout, MyServer.timerTimeout);
			}
		}
		/// 
		/// Decription: Stop the threads for the port listener.
		/// 
		public void Stop()
		{
			int lcv;
			lostTimer.Dispose();
			lostTimer = null;

			for (lcv = 0; lcv < numThreads; lcv++)
			{
				if (!serverThread[lcv].IsAlive)
					threadEnd[lcv].Set();	// Set event if thread is already dead
			}
			ShuttingDown = true;
			// Create a connection to the port to unblock the listener thread
			Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, this.portNumber);
			sock.Connect(endPoint);
			//sock.Close();
			sock = null;

			// Check thread end events and wait for up to 5 seconds.
			for (lcv = 0; lcv < numThreads; lcv++)
				threadEnd[lcv].WaitOne(5000, false);
		}

		/// 
		/// Decription: Open a listener socket and wait for a connection.
		/// 
		private void StartListening()
		{
			// Establish the local endpoint for the socket.
			IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, this.portNumber);
			// Create a TCP/IP socket.
			Socket listener = new Socket(AddressFamily.InterNetwork,
				SocketType.Stream, ProtocolType.Tcp);




			// Bind the socket to the local endpoint and listen for incoming connections.


			try
			{
				listener.Bind(localEndPoint);
				listener.Listen(1000);

				while (!ShuttingDown)
				{
					// Set the event to nonsignaled state.
					allDone.Reset();
					// Start an asynchronous socket to listen for connections.
	
					listener.BeginAccept(new AsyncCallback(this.AcceptCallback),listener);

					// Wait until a connection is made before continuing.
					allDone.WaitOne();
				}
			}
			catch (Exception e)
			{
				threadEnd[0].Set();
			}
		}
		/// 
		/// Decription: Call back method to accept new connections.
		/// 
		/// <param name="ar">Status of an asynchronous operation.</param>
		private void AcceptCallback(IAsyncResult ar)
		{
			// Signal the main thread to continue.
			allDone.Set();
			// Get the socket that handles the client request.
			Socket listener = (Socket) ar.AsyncState;
			Socket handler = listener.EndAccept(ar);

			// Create the state object.
			StateObject state = new StateObject();
			state.workSocket = handler;
			state.TimeStamp = DateTime.Now;

			try
			{
				Interlocked.Increment(ref sockCount);
				Monitor.Enter(connectedSocks);
				connectedSocks.Add(state);
				Monitor.Exit(connectedSocks);

				handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,new AsyncCallback(this.ReadCallback), state);
				if (sockCount > this.maxSockets)
				{
					RemoveSocket(state);
					//handler.Shutdown(SocketShutdown.Both);
					//handler.Close();
					handler = null;
					state = null;
				}
			}
			catch (SocketException es)
			{
				RemoveSocket(state);
			}
			catch (Exception e)
			{
				RemoveSocket(state);
			}
		}

		/// 
		/// Decription: Call back method to handle incoming data.
		/// 
		/// <param name="ar">Status of an asynchronous operation.</param>
		protected void ReadCallback(IAsyncResult ar)
		{
			String content = String.Empty;
			// Retrieve the state object and the handler socket
			// from the async state object.
			StateObject state = (StateObject) ar.AsyncState;
			Socket handler = state.workSocket;
			try
			{
				// Read data from the client socket.
				int bytesRead = handler.EndReceive(ar);

				if (bytesRead > 0)
				{
					Monitor.Enter(state);
					string receive = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
					Monitor.Exit(state);
					if(m_ReceiveFunc!=null)
					{
						m_ReceiveFunc(receive);
					}
					handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,new AsyncCallback(this.ReadCallback), state);
				}
				else
				{	// Disconnected
					RemoveSocket(state);
				}
			}
			catch (System.Net.Sockets.SocketException es)
			{
				RemoveSocket(state);
				if (es.ErrorCode != 64)
				{
					Console.WriteLine( string.Format("ReadCallback Socket Exception: {0}, {1}.",es.ErrorCode, es.ToString()));
				}
			}
			catch (Exception e)
			{
				RemoveSocket(state);
				if (e.GetType().FullName != "System.ObjectDisposedException")
				{
					Console.WriteLine(string.Format("ReadCallback Exception: {0}.", e.ToString()));
				}
			}
		}

		/// 
		/// Decription: Send the given data string to the given socket.
		/// 
		/// <param name="sock">Socket to send data to.</param>
		/// <param name="data">The string containing the data to send.</param>
		protected void Send(Socket sock, string data)
		{
			// Convert the string data to byte data using ASCII encoding.
			byte[] byteData = Encoding.ASCII.GetBytes(data);

			// Begin sending the data to the remote device.
			if (byteData.Length > 0)
				sock.BeginSend(byteData, 0, byteData.Length, 0,
					new AsyncCallback(this.SendCallback), sock);
		}

		/// 
		/// Decription: Call back method to handle outgoing data.
		/// 
		/// <param name="ar">Status of an asynchronous operation.</param>
		protected void SendCallback(IAsyncResult ar)
		{
			// Retrieve the socket from the async state object.
			Socket handler = (Socket) ar.AsyncState;
			try
			{
				// Complete sending the data to the remote device.
				int bytesSent = handler.EndSend(ar);
			}
			catch (Exception e)
			{
			}
		}

		/// 
		// Description: Find on socket using the identifier as the key.
		/// 
		/// <param name="id">The identifier key associated with a socket.</param>

		private Socket FindID(string id)
		{
			Socket sock = null;
			Monitor.Enter(connectedHT);
			if (connectedHT.ContainsKey(id))
				sock = (Socket) connectedHT[id];
			Monitor.Exit(connectedHT);
			return sock;
		}

		/// 
		/// Description: Remove the socket contained in the given state object
		/// from the connected array list and hash table, then close the socket.
		/// 
		/// <param name="state">The StateObject containing the specific socket
		/// to remove from the connected array list and hash table.</param>
		virtual protected void RemoveSocket(StateObject state)
		{
			Socket sock = state.workSocket;
			Monitor.Enter(connectedSocks);
			if (connectedSocks.Contains(state))
			{
				connectedSocks.Remove(state);
				Interlocked.Decrement(ref sockCount);
			}
			Monitor.Exit(connectedSocks);
			Monitor.Enter(connectedHT);

			if ((sock != null) && (connectedHT.ContainsKey(sock)))
			{
				object sockTemp = connectedHT[sock];
				if (connectedHT.ContainsKey(sockTemp))
				{
					if (connectedHT.ContainsKey(connectedHT[sockTemp]))
					{
						connectedHT.Remove(sock);
						if (sock.Equals(connectedHT[sockTemp]))
						{
							connectedHT.Remove(sockTemp);
						}
						else
						{
							object val, key = sockTemp;
							while (true)
							{
								val = connectedHT[key];
								if (sock.Equals(val))
								{
									connectedHT[key] = sockTemp;
									break;
								}
								else if (connectedHT.ContainsKey(val))
									key = val;
								else	// The chain is broken
									break;
							}
						}
					}
					else
					{
						Console.WriteLine(string.Format("Socket is not in the {0} connected hash table!",	this.title));
					}
				}
			}
			Monitor.Exit(connectedHT);

			if (sock != null)
			{
				//if (sock.Connected)
				//    sock.Shutdown(SocketShutdown.Both);
				sock.Close();
				sock = null;
				state.workSocket = null;
				state = null;
		
			}
		}
	}
}
