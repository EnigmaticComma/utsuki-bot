namespace App.Extensions {
	public static class TaskExtensions {
		public static async void Forget(this Task task)
		{
			try {
				await task;
			}
			catch (Exception e) {
				Console.WriteLine(e);
			}
		}
	}
}
