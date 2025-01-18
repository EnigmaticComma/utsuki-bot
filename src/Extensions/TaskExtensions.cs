using System.Threading.Tasks;

namespace App.Extensions {
	public static class TaskExtensions {
		public static async void CAwait(this Task task) {
			await task;
		}
	}
}
