using SmartBear.Collaborator.API;
using System;

namespace SmartBear.Collaborator.Script.CustomLog
{
	class CustomLogger : ILogger
	{
		/// <summary>
		/// Designer of the custom logger. Designer should be implemented 
		/// if you want to use your own logger
		/// </summary>
		/// <param name="logClass">Type of structure where logger will be used</param>
		public CustomLogger(Type logClass)
		{

		}

		public bool IsShowErrorMessage
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public string CustomPathToLogFolder
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public void LogDebugMessage(string message)
		{
			throw new NotImplementedException();
		}

		public void LogDebugMessage(string message, Exception exception)
		{
			throw new NotImplementedException();
		}

		public void LogError(string message)
		{
			throw new NotImplementedException();
		}

		public void LogException(Exception exception)
		{
			throw new NotImplementedException();
		}

		public void LogInfoMessage(string message)
		{
			throw new NotImplementedException();
		}

		public void LogWarningMessage(string message)
		{
			throw new NotImplementedException();
		}

		public void ShowError(string error)
		{
			throw new NotImplementedException();
		}
	}
}
