using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Extensions {
		
	public static class IEnumerableExtensions {
		public static T RandomElement<T>(this IEnumerable<T> enumerable) {
			var array = enumerable as T[] ?? enumerable.ToArray();
			if (array.Length <= 0) return default;
			int index = new Random().Next(0, array.Length);
			return array.ElementAt(index);
		}
	}
		
}
