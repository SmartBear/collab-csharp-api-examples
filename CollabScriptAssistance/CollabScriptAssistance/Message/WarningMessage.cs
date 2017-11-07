namespace SmartBear.Collaborator.Message
{
	class WarningMessage : IMessage
	{
		public string MessageText { get; set; }

		public WarningMessage(string message)
		{
			MessageText = "Warning: " + message;
		}
	}
}
