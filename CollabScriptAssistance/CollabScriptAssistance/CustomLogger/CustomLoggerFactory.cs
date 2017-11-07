using SmartBear.Collaborator.API;
using System;

namespace SmartBear.Collaborator.Script.CustomLog
{
	class CustomLoggerFactory : ILoggerFactory
	{
		public ILogger GetLogger(Type logClass)
		{
			return new CustomLogger(logClass);
		}
	}
}
