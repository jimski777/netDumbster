#region Header

// Copyright (c) 2010, Hexasystems Corporation
// All rights reserved.

#endregion Header

namespace netDumbster.smtp
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using netDumbster.smtp.Logging;

    /// <summary>
    /// Simple Smtp Server
    /// </summary>
    public class SimpleSmtpServer
    {
        #region Fields

        /// <summary>
        /// Logger
        /// </summary>
        ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Stores all of the email received since this instance started up.
        /// </summary>
        private ConcurrentBag<SmtpMessage> smtpMessageStore = new ConcurrentBag<SmtpMessage>();

        /// <summary>
        ///  Flag to stop server
        /// </summary>
        private volatile bool stop;

        /// <summary>
        /// TCP Listener
        /// </summary>
        private TcpListener tcpListener;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleSmtpServer"/> class.
        /// </summary>
        /// <param name="port">The port.</param>
        private SimpleSmtpServer(int port) 
            : this(port, true)
        {
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="SimpleSmtpServer"/> class from being created.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="useMessageStore">if set to <c>true</c> [use message store].</param>
        private SimpleSmtpServer(int port, bool useMessageStore)
        {
            this.UseMessageStore = useMessageStore;
            this.stop = false;
            Port = port;
            ServerReady = new AutoResetEvent(false);
        }

        #endregion Constructors

        #region Events

        public event EventHandler<MessageReceivedArgs> MessageReceived;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        /// <value>The port.</value>
        public int Port
        {
            get;
            private set;
        }

        /// <summary>
        /// List of email received by this instance since start up.
        /// </summary>
        /// <value><see cref="T:System.Array">Array</see> holding received <see cref="T:netDumbster.smtp.SmtpMessage">SmtpMessage</see></value>
        public virtual SmtpMessage[] ReceivedEmail
        {
            get
            {
                lock (this)
                {
                    return this.smtpMessageStore.ToArray();
                }
            }
        }

        /// <summary>
        /// Number of messages received by this instance since start up.
        /// </summary>
        /// <value>Number of messages</value>
        public virtual int ReceivedEmailCount
        {
            get
            {
                lock (this)
                {
                    return this.smtpMessageStore.Count;
                }
            }
        }

        internal AutoResetEvent ServerReady
        {
            get;
            set;
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Gets the random unused port.
        /// </summary>
        /// <returns></returns>
        public static int GetRandomUnusedPort()
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        /// <returns></returns>
        public static SimpleSmtpServer Start()
        {
            int port = GetRandomUnusedPort();
            return SimpleSmtpServer.Start(port, true);
        }

        /// <summary>
        /// Starts the specified use message store.
        /// </summary>
        /// <param name="useMessageStore">if set to <c>true</c> [use message store].</param>
        /// <returns></returns>
        public static SimpleSmtpServer Start(bool useMessageStore)
        {
            int port = GetRandomUnusedPort();
            return SimpleSmtpServer.Start(port, useMessageStore);
        }

        /// <summary>
        /// Starts server listening to the specified port.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <returns></returns>
        public static SimpleSmtpServer Start(int port)
        {
            return SimpleSmtpServer.Start(port, true);
        }

        /// <summary>
        /// Starts the specified port.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="useMessageStore">if set to <c>true</c> [use message store].</param>
        /// <returns></returns>
        public static SimpleSmtpServer Start(int port, bool useMessageStore)
        {
            var server = new SimpleSmtpServer(port, useMessageStore);
            new Thread(new ThreadStart(server.StartListening)).Start();
            server.ServerReady.WaitOne();
            return server;
        }

        /// <summary>
        /// Clears the received email.
        /// </summary>
        public void ClearReceivedEmail()
        {
            lock (this)
            {
                SmtpMessage itemToRemove;
                while (!this.smtpMessageStore.IsEmpty)
                {
                    this.smtpMessageStore.TryTake(out itemToRemove);
                }
            }
        }

        /// <summary>
        /// Stop the server.  This notifies the listener to stop accepting new connections
        /// and that the loop should exit.
        /// </summary>
        public void Stop()
        {
            log.Debug("Trying to stop SmtpServer.");

            try
            {
                lock(this)
                {
                    this.stop = true;

                    // Kick the server accept loop
                    if (tcpListener != null)
                    {
                        //_processor.Stop();
                        log.Debug("Stopping tcp listener.");
                        tcpListener.Stop();
                        log.Debug("Tcp listener stopped.");
                    }
                    tcpListener = null;
                }
            }
            catch (Exception ex)
            {
                log.Warn("Unexpected Exception stopping SmtpServer", ex);
            }
            finally
            {
                log.Debug("SmtpServer Stopped.");
                ServerReady.Close();
            }
        }

        /// <summary>
        /// Starts the Server
        /// </summary>
        internal void StartListening()
        {
            log.Info("Starting Smtp server");

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, Port);
            tcpListener = new TcpListener(endPoint);
            // Fix the problem with the scenario if the server is stopped, and then
            // restarted with the same port, it will not throw an error.
            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            tcpListener.Start();

            log.DebugFormat("Started Tcp Listener at port {0}", Port);

            try
            {
                log.Debug("Calling BeginAcceptSocket.");
                tcpListener.BeginAcceptSocket(new AsyncCallback(_SocketHandler), tcpListener);
                log.Debug("BeginAcceptSocket called.");
                ServerReady.Set();
            }
            catch (Exception ex)
            {
                log.Warn("Unexpected Exception starting the SmtpServer.", ex);
            }
        }

        /// <summary>
        /// Gets a value indicating whether [use message store].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [use message store]; otherwise, <c>false</c>.
        /// </value>
        public bool UseMessageStore
        {
            get;
            private set;
        }

        /// <summary>
        /// Async Socket handler.
        /// </summary>
        /// <param name="result">The result.</param>
        private void _SocketHandler(IAsyncResult result)
        {
            if (this.stop)
            {
                return;
            }

            log.Debug("Entering Socket Handler.");

            try
            {
                TcpListener listener = (TcpListener)result.AsyncState;
                listener.BeginAcceptSocket(new AsyncCallback(_SocketHandler), listener);

                log.Debug("Calling EndAcceptSocket.");

                using (Socket socket = listener.EndAcceptSocket(result))
                {
                    log.Debug("Socket accepted and ready to be processed.");
                    SmtpProcessor processor = new SmtpProcessor(string.Empty, this.UseMessageStore ? this.smtpMessageStore : null);
                    processor.MessageReceived += (sender, args) =>
                    {
                        if (MessageReceived != null)
                        {
                            MessageReceived(this, args);
                        }
                    };
                    processor.ProcessConnection(socket);
                }
            }
            catch (ObjectDisposedException ex)
            {
                log.Warn("Object Disposed Exception. THIS IS EXPECTED ONLY IF SERVER WAS STOPPED.", ex);
            }
            catch (SocketException ex)
            {
                log.Warn("Socket Exception", ex);
            }
        }

        #endregion Methods
    }
}