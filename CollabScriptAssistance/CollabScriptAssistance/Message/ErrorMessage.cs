namespace SmartBear.Collaborator.Message
{
	class ErrorMessage : IMessage
	{
		public string MessageText { get; set; }

		public ErrorMessage(string message)
		{
			MessageText = "Error: " + message;
		}
	}
}
