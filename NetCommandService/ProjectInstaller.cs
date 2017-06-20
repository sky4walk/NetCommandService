using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Runtime.InteropServices;

namespace NetCommandService
{
	/// <summary>
	/// Summary description for ProjectInstaller.
	/// </summary>
	[RunInstaller(true)]
	public class ProjectInstaller : System.Configuration.Install.Installer
	{
		private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller1;
		private System.ServiceProcess.ServiceInstaller serviceInstaller1;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public ProjectInstaller()
		{
			// This call is required by the Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}


		private void ProjectInstaller_AfterInstall(object sender, 
			System.Configuration.Install.InstallEventArgs e)
		{
			//Our code goes in this event because it is the only one that will do
			//a proper job of letting the user know that an error has occurred,
			//if one indeed occurs. Installation will be rolled back if an error occurs.

			int iSCManagerHandle = 0;
			int iSCManagerLockHandle = 0;
			int iServiceHandle = 0;
			bool bChangeServiceConfig = false;
			bool bChangeServiceConfig2 = false;
			modAPI.SERVICE_DESCRIPTION ServiceDescription;
			modAPI.SERVICE_FAILURE_ACTIONS ServiceFailureActions;
			modAPI.SC_ACTION[] ScActions = new modAPI.SC_ACTION[3];	//There should be one element for each
		                                                            //action. The Services snap-in shows 3 possible actions.

			bool bCloseService = false;
			bool bUnlockSCManager = false;
			bool bCloseSCManager = false;

			IntPtr iScActionsPointer = new IntPtr();

			try
			{
				//Obtain a handle to the Service Control Manager, with appropriate rights.
				//This handle is used to open the relevant service.
				iSCManagerHandle = modAPI.OpenSCManagerA(null, null, 
					modAPI.ServiceControlManagerType.SC_MANAGER_ALL_ACCESS);

				//Check that it's open. If not throw an exception.
				if (iSCManagerHandle < 1)
				{
					throw new Exception("Unable to open the Services Manager.");
				}

                //Lock the Service Control Manager database.
				iSCManagerLockHandle = modAPI.LockServiceDatabase(iSCManagerHandle);

				//Check that it's locked. If not throw an exception.
				if (iSCManagerLockHandle < 1)
				{
					throw new Exception("Unable to lock the Services Manager.");
				}

				//Obtain a handle to the relevant service, with appropriate rights.
				//This handle is sent along to change the settings. The second parameter
				//should contain the name you assign to the service.
				iServiceHandle = modAPI.OpenServiceA(iSCManagerHandle, "CommandService",
					modAPI.ACCESS_TYPE.SERVICE_ALL_ACCESS);

				//Check that it's open. If not throw an exception.
				if (iServiceHandle < 1)
				{
					throw new Exception("Unable to open the Service for modification.");
				}
	
				//Call ChangeServiceConfig to update the ServiceType to SERVICE_INTERACTIVE_PROCESS.
				//Very important is that you do not leave out or change the other relevant
				//ServiceType settings. The call will return False if you do.
				//Also, only services that use the LocalSystem account can be set to
				//SERVICE_INTERACTIVE_PROCESS.
				bChangeServiceConfig = modAPI.ChangeServiceConfigA(iServiceHandle,
					modAPI.ServiceType.SERVICE_WIN32_OWN_PROCESS | modAPI.ServiceType.SERVICE_INTERACTIVE_PROCESS,
					modAPI.SERVICE_NO_CHANGE, modAPI.SERVICE_NO_CHANGE, null, null,
					0, null, null, null, null);

				//If the call is unsuccessful, throw an exception.
				if (bChangeServiceConfig==false)
				{
					throw new Exception("Unable to change the Service settings.");
				}

				//To change the description, create an instance of the SERVICE_DESCRIPTION
				//structure and set the lpDescription member to your desired description.
				ServiceDescription.lpDescription = "Command center for remote starting";

				//Call ChangeServiceConfig2 with SERVICE_CONFIG_DESCRIPTION in the second
				//parameter and the SERVICE_DESCRIPTION instance in the third parameter
				//to update the description.
				bChangeServiceConfig2 = modAPI.ChangeServiceConfig2A(iServiceHandle,
					modAPI.InfoLevel.SERVICE_CONFIG_DESCRIPTION,ref ServiceDescription);

				//If the update of the description is unsuccessful it is up to you to
				//throw an exception or not. The fact that the description did not update
				//should not impact the functionality of your service.
				if (bChangeServiceConfig2==false)
				{
					throw new Exception("Unable to set the Service description.");
				}		

				//To change the Service Failure Actions, create an instance of the
				//SERVICE_FAILURE_ACTIONS structure and set the members to your
				//desired values. See MSDN for detailed descriptions.
				ServiceFailureActions.dwResetPeriod = 600;
				ServiceFailureActions.lpRebootMsg = "Service failed to start! Rebooting...";
				ServiceFailureActions.lpCommand = "SomeCommand.exe Param1 Param2";
				ServiceFailureActions.cActions = ScActions.Length;

				//The lpsaActions member of SERVICE_FAILURE_ACTIONS is a pointer to an
				//array of SC_ACTION structures. This complicates matters a little,
				//and although it took me a week to figure it out, the solution
				//is quite simple.

				//First order of business is to populate our array of SC_ACTION structures
				//with appropriate values.
				ScActions[0].Delay = 20000;
				ScActions[0].SCActionType = modAPI.SC_ACTION_TYPE.SC_ACTION_RESTART;
				ScActions[1].Delay = 20000;
				ScActions[1].SCActionType = modAPI.SC_ACTION_TYPE.SC_ACTION_RUN_COMMAND;
				ScActions[2].Delay = 20000;
				ScActions[2].SCActionType = modAPI.SC_ACTION_TYPE.SC_ACTION_REBOOT;

				//Once that's done, we need to obtain a pointer to a memory location
				//that we can assign to lpsaActions in SERVICE_FAILURE_ACTIONS.
				//We use 'Marshal.SizeOf(New modAPI.SC_ACTION) * 3' because we pass 
				//3 actions to our service. If you have less actions change the * 3 accordingly.
				iScActionsPointer = Marshal.AllocHGlobal(Marshal.SizeOf(new modAPI.SC_ACTION()) * 3);

				//Once we have obtained the pointer for the memory location we need to
				//fill the memory with our structure. We use the CopyMemory API function
				//for this. Please have a look at it's declaration in modAPI.
				modAPI.CopyMemory(iScActionsPointer, ScActions, Marshal.SizeOf(new modAPI.SC_ACTION()) * 3);

				//We set the lpsaActions member of SERVICE_FAILURE_ACTIONS to the integer
				//value of our pointer.
				ServiceFailureActions.lpsaActions = iScActionsPointer.ToInt32();

				//We call bChangeServiceConfig2 with the relevant parameters.
				bChangeServiceConfig2 = modAPI.ChangeServiceConfig2A(iServiceHandle,
				modAPI.InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,ref ServiceFailureActions);

				//If the update of the failure actions are unsuccessful it is up to you to
				//throw an exception or not. The fact that the failure actions did not update
				//should not impact the functionality of your service.
				if (bChangeServiceConfig2==false)
				{
					throw new Exception("Unable to set the Service Failure Actions.");
				}
							}
			catch(Exception ex)
			{
				//Throw the exception again so the installer can get to it
				throw new Exception(ex.Message);
			}
			finally
			{
				//Close the handles if they are open.
				Marshal.FreeHGlobal(iScActionsPointer);

				if (iServiceHandle > 0)
				{
					bCloseService = modAPI.CloseServiceHandle(iServiceHandle);
				}

				if (iSCManagerLockHandle > 0)
				{
					bUnlockSCManager = modAPI.UnlockServiceDatabase(iSCManagerLockHandle);
				}

				if (iSCManagerHandle != 0)
				{
					bCloseSCManager = modAPI.CloseServiceHandle(iSCManagerHandle);
				}
			}
			//When installation is done go check out your handy work using Computer Management!
		}
	#region Component Designer generated code
	/// <summary>
	/// Required method for Designer support - do not modify
	/// the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent()
{
		this.serviceProcessInstaller1 = new System.ServiceProcess.ServiceProcessInstaller();
		this.serviceInstaller1 = new System.ServiceProcess.ServiceInstaller();
		// 
		// serviceProcessInstaller1
		// 
		this.serviceProcessInstaller1.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
		this.serviceProcessInstaller1.Password = null;
		this.serviceProcessInstaller1.Username = null;
		// 
		// serviceInstaller1
		// 
		this.serviceInstaller1.DisplayName = "CommandService";
		this.serviceInstaller1.ServiceName = "CommandService";
		this.serviceInstaller1.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
		// 
		// ProjectInstaller
		// 
		this.Installers.AddRange(new System.Configuration.Install.Installer[] {
																				  this.serviceProcessInstaller1,
																				  this.serviceInstaller1});
		this.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.ProjectInstaller_AfterInstall);

	}
		#endregion
	}
}
