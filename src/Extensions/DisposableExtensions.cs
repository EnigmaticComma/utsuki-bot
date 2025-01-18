using System;
using System.Reactive.Disposables;

namespace App.Extensions {
	public static class DisposableExtensions {
		public static void AddTo(this IDisposable disposable, CompositeDisposable compositeDisposable) {
			compositeDisposable.Add(disposable);
		}
	}
}
