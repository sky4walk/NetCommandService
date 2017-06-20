//André Betz 2004
// http://www.andrebetz.de
using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;

namespace NetCommandService
{
	public class CommandService : System.ServiceProcess.ServiceBase
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private MyServer m_myServer = null;
		private StringBuilder m_sb = null;

		public CommandService()
		{
			// This call is required by the Windows.Forms Component Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitComponent call
		}

		// The main entry point for the process
		static void Main()
		{
			System.ServiceProcess.ServiceBase[] ServicesToRun;
	
			// More than one user Service may run within the same process. To add
			// another service to this process, change the following line to
			// create a second service object. For example,
			//
			//   ServicesToRun = new System.ServiceProcess.ServiceBase[] {new Service1(), new MySecondUserService()};
			//
			ServicesToRun = new System.ServiceProcess.ServiceBase[] { new CommandService() };

			System.ServiceProcess.ServiceBase.Run(ServicesToRun);

		}

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
			m_myServer = new MyServer(11017,"CommandServiceServer",new SocketReceiver(ReceiveProc));
			this.ServiceName = "CommandService";
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		/// <summary>
		/// Set things in motion so your service can do its work.
		/// </summary>
		protected override void OnStart(string[] args)
		{
			m_sb = new StringBuilder();
			m_myServer.Start();
		}
 
		/// <summary>
		/// Stop this service.
		/// </summary>
		protected override void OnStop()
		{
			m_myServer.Stop();
		}
		
		/// <summary>
		/// Receive Proc
		/// </summary>
		public void ReceiveProc(string sb)
		{
			if(sb.Equals("\r\n"))
			{
				try
				{
					string CommandLine = m_sb.ToString();
					string[] splitted = CommandLine.Split(new char[]{'&'});
					// Aufbau: Pfad & Kommando mit Pfad & Argumente
					if(splitted!=null && splitted.Length==3)
					{
						Process process = new Process();
						process.StartInfo.RedirectStandardOutput = false;
						process.StartInfo.RedirectStandardError = false;
						process.StartInfo.RedirectStandardInput = false;
						process.StartInfo.UseShellExecute = false;
						process.StartInfo.CreateNoWindow = false;
						process.StartInfo.WorkingDirectory = splitted[0];
						process.StartInfo.FileName = splitted[0]+splitted[1];
						process.StartInfo.Arguments = splitted[2];
						process.Start();
					}
				}
				catch
				{
				}
				m_sb = new StringBuilder();
			}
			else
			{
				m_sb.Append(sb);
			}
		}
	}
}
